namespace Orvano.Server;

/// <summary>One problem with one field of the request, from a <c>validation_failed</c> answer (spec 0014 PRB-3).</summary>
/// <param name="Field">The field's wire name; in a body, a dotted camelCase path with indexes (<c>address.city</c>).</param>
/// <param name="Location">Where the field is: <c>body</c>, <c>query</c>, <c>path</c>, or <c>header</c>.</param>
/// <param name="Code">What is wrong, such as <c>required</c> or <c>too_long</c>.</param>
/// <param name="Message">An English sentence describing the problem, safe to show.</param>
public sealed record OrvanoFieldError(string Field, string Location, string Code, string Message);
