using System;
using System.Collections.Generic;
using System.Linq;

namespace LANCommander.SDK.Models;

/// <summary>
/// The password rules the server enforces during registration, so clients can show them before
/// the user submits rather than only reporting them as validation failures afterwards.
/// </summary>
public class PasswordPolicy
{
    public int RequiredLength { get; set; }
    public bool RequireDigit { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireNonAlphanumeric { get; set; }

    /// <summary>
    /// Builds a single sentence describing the policy, or an empty string when nothing is enforced.
    /// </summary>
    public string Describe()
    {
        var requirements = new List<string>();

        if (RequiredLength > 0)
            requirements.Add($"be at least {RequiredLength} characters");

        var characterClasses = new List<string>();

        if (RequireDigit)
            characterClasses.Add("a number");

        if (RequireLowercase)
            characterClasses.Add("a lowercase letter");

        if (RequireUppercase)
            characterClasses.Add("an uppercase letter");

        if (RequireNonAlphanumeric)
            characterClasses.Add("a symbol");

        if (characterClasses.Count > 0)
            requirements.Add("include " + Join(characterClasses));

        if (requirements.Count == 0)
            return String.Empty;

        return "Password must " + Join(requirements) + ".";
    }

    private static string Join(IReadOnlyList<string> parts)
    {
        if (parts.Count == 1)
            return parts[0];

        if (parts.Count == 2)
            return $"{parts[0]} and {parts[1]}";

        return String.Join(", ", parts.Take(parts.Count - 1)) + ", and " + parts[parts.Count - 1];
    }
}
