using System.Reflection;
using System.Reflection.Emit;
using Aquila.Controls.Primitives;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The drawing primitives (<c>Aquila.Controls.Primitives</c>) know WPF and nothing of Aquila — no preset, sensor,
/// setting or piece — so they can move to a project of their own, and whatever wraps them decides colours and timing
/// (docs/graficos-proprios).
///
/// Held by reading what the primitives' code actually uses, not by looking for <c>using</c> lines: the namespace sits
/// inside <c>Aquila.Controls</c>, so <c>Motion.Speed</c> or <c>Aquila.Models.Ramp</c> would compile there with no
/// using at all. Every signature is read by reflection and every method body's IL for the types, fields and methods
/// it names. Until the primitives have a project of their own and the compiler guards the line, this does.
/// </summary>
public sealed class PrimitivesBoundary
{
    private const string Home = "Aquila.Controls.Primitives";

    private static readonly Assembly Aquila = typeof(BarFill).Assembly;

    [Fact]
    public void The_primitives_use_nothing_of_Aquila_outside_them()
    {
        var primitives = Aquila.GetTypes().Where(type => type.Namespace == Home).ToList();
        Assert.Contains(typeof(LinePlot), primitives);

        var crossings = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in primitives)
        {
            foreach (var used in Signatures(type)) Note(type, used, crossings);
            foreach (var body in Bodies(type))
                foreach (var used in Named(body))
                    Note(type, used, crossings);
        }

        Assert.Empty(crossings);
    }

    /// <summary>A type of Aquila's own, outside the primitives — compiler-made helpers (names in angle brackets) aside.
    /// </summary>
    private static void Note(Type within, Type? used, ISet<string> crossings)
    {
        foreach (var type in Parts(used))
            if (type.Assembly == Aquila && type.Namespace != Home && !type.Name.StartsWith('<'))
                crossings.Add($"{within.FullName} uses {type.FullName}");
    }

    /// <summary>A type and every type it is made of: an array's element, a generic's arguments.</summary>
    private static IEnumerable<Type> Parts(Type? type)
    {
        if (type is null || type.IsGenericParameter) yield break;

        if (type.HasElementType)
        {
            foreach (var part in Parts(type.GetElementType())) yield return part;
            yield break;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var part in Parts(argument))
                    yield return part;
    }

    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
        BindingFlags.Static;

    /// <summary>Every type a type's shape names: what it derives from and implements, its fields, properties, events,
    /// methods' parameters and results, and the attributes on all of them.</summary>
    private static IEnumerable<Type?> Signatures(Type type)
    {
        yield return type.BaseType;
        foreach (var contract in type.GetInterfaces()) yield return contract;
        foreach (var attribute in type.CustomAttributes) yield return attribute.AttributeType;

        foreach (var member in type.GetMembers(Declared))
        {
            foreach (var attribute in member.CustomAttributes) yield return attribute.AttributeType;

            switch (member)
            {
                case FieldInfo field: yield return field.FieldType; break;
                case PropertyInfo property: yield return property.PropertyType; break;
                case EventInfo @event: yield return @event.EventHandlerType; break;
                case MethodBase method:
                    if (method is MethodInfo result) yield return result.ReturnType;
                    foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
                    break;
            }
        }
    }

    private static IEnumerable<MethodBase> Bodies(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
            .Where(method => method.GetMethodBody() is not null);

    /// <summary>Every type a method body names: its locals, and every type, field and method its IL takes a token for
    /// — with the types each field and method is declared on and made of.</summary>
    private static IEnumerable<Type?> Named(MethodBase method)
    {
        var body = method.GetMethodBody()!;
        foreach (var local in body.LocalVariables) yield return local.LocalType;

        var il = body.GetILAsByteArray() ?? [];
        var typeArguments = method.DeclaringType is { IsGenericType: true } owner ? owner.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        for (var at = 0; at < il.Length;)
        {
            var code = il[at] == 0xFE ? TwoByte[il[at + 1]] : OneByte[il[at]];
            at += code.Size;

            switch (code.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    at += 1;
                    break;
                case OperandType.InlineVar:
                    at += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    at += 8;
                    break;
                case OperandType.InlineSwitch:
                    at += 4 + 4 * BitConverter.ToInt32(il, at);
                    break;
                case OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok
                    or OperandType.InlineType:
                    foreach (var type in Of(Resolve(method.Module, BitConverter.ToInt32(il, at), typeArguments,
                                 methodArguments)))
                        yield return type;
                    at += 4;
                    break;
                default:
                    at += 4;
                    break;
            }
        }
    }

    private static MemberInfo? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
    {
        try
        {
            return module.ResolveMember(token, typeArguments, methodArguments);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IEnumerable<Type?> Of(MemberInfo? member)
    {
        switch (member)
        {
            case Type type:
                yield return type;
                break;
            case FieldInfo field:
                yield return field.DeclaringType;
                yield return field.FieldType;
                break;
            case MethodBase method:
                yield return method.DeclaringType;
                if (method is MethodInfo result) yield return result.ReturnType;
                foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
                break;
        }
    }

    private static readonly OpCode[] OneByte = new OpCode[256];
    private static readonly OpCode[] TwoByte = new OpCode[256];

    static PrimitivesBoundary()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var code = (OpCode)field.GetValue(null)!;
            (code.Size == 1 ? OneByte : TwoByte)[code.Value & 0xFF] = code;
        }
    }
}
