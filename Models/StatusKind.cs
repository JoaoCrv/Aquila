namespace Aquila.Models;

/// <summary>
/// What a message is reporting, apart from the words it uses.
///
/// Classified where the message is WRITTEN, never matched on the text afterwards: only the writer knows
/// whether "Update available" is good news or a nudge, and matching strings breaks the first time somebody
/// rewords one — or ships a translation.
///
/// Plain is the default and should stay the common case. A screen where everything is coloured has said
/// nothing about any of it.
/// </summary>
public enum StatusKind
{
    Plain,
    Good,
    Caution,
    Bad,
}
