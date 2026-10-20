using System.Text.Encodings.Web;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace LANCommander.Server.UI.TagHelpers;

/// <summary>
/// Styles plain HTML in MVC views and Razor Pages (e.g. the account pages, which post ordinary forms)
/// like the matching Controls, so those pages never write the component library's classes themselves.
/// Modifiers are bare attributes, as on the Controls: <c>&lt;button lc-button lc-primary&gt;</c>.
/// </summary>
internal static class StaticModifiers
{
    /// <summary>Whether a bare modifier attribute is present, removing it from the output either way.</summary>
    public static bool Take(TagHelperContext context, TagHelperOutput output, string name)
    {
        output.Attributes.RemoveAll(name);

        return context.AllAttributes.ContainsName(name);
    }

    public static void AddClasses(TagHelperOutput output, string? classes)
    {
        if (classes == null)
            return;

        foreach (var cls in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            output.AddClass(cls, HtmlEncoder.Default);
    }
}

/// <summary>
/// A button or link drawn like the Button control. <c>lc-primary</c>, <c>lc-text</c>, <c>lc-link</c> and
/// <c>lc-danger</c> pick the variant as they do on Button; <c>lc-small</c>/<c>lc-large</c> size it and
/// <c>lc-block</c> stretches it to the full width.
/// </summary>
[HtmlTargetElement("button", Attributes = "lc-button")]
[HtmlTargetElement("a", Attributes = "lc-button")]
public class ButtonTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("lc-button");

        var primary = StaticModifiers.Take(context, output, "lc-primary");
        var text = StaticModifiers.Take(context, output, "lc-text");
        var link = StaticModifiers.Take(context, output, "lc-link");
        var danger = StaticModifiers.Take(context, output, "lc-danger");
        var small = StaticModifiers.Take(context, output, "lc-small");
        var large = StaticModifiers.Take(context, output, "lc-large");
        var block = StaticModifiers.Take(context, output, "lc-block");

        // Same precedence as the Button control: Primary, then Link, then Text, then outlined
        var (variant, style) = (primary, link, text) switch
        {
            (true, _, _) => ("rz-variant-filled", danger ? "rz-danger" : "rz-primary"),
            (_, true, _) => ("rz-variant-text", danger ? "rz-danger" : "rz-primary"),
            (_, _, true) => ("rz-variant-text", danger ? "rz-danger" : "rz-base"),
            _ => ("rz-variant-outlined", danger ? "rz-danger" : "rz-base"),
        };

        StaticModifiers.AddClasses(output, new ClassBuilder()
            .Add("rz-button")
            .Add(small ? "rz-button-sm" : large ? "rz-button-lg" : "rz-button-md")
            .Add(variant)
            .Add(style)
            .Add("rz-shade-default")
            .Add("lc-button")
            .If(link, "lc-button-link")
            .If(block, "lc-button-block")
            .Build());

        output.PreContent.AppendHtml(new HtmlString("<span class=\"rz-button-box\"><span class=\"lc-button-text\">"));
        output.PostContent.AppendHtml(new HtmlString("</span></span>"));
    }
}

/// <summary>A message box drawn like the Alert control: <c>lc-success</c>, <c>lc-warning</c> or <c>lc-danger</c>, informational otherwise.</summary>
[HtmlTargetElement("div", Attributes = "lc-alert")]
public class AlertTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("lc-alert");

        var status = StatusResolver.Resolve(
            success: StaticModifiers.Take(context, output, "lc-success"),
            info: StaticModifiers.Take(context, output, "lc-info"),
            warning: StaticModifiers.Take(context, output, "lc-warning"),
            danger: StaticModifiers.Take(context, output, "lc-danger"));

        if (status == Status.None)
            status = Status.Info;

        StaticModifiers.AddClasses(output, $"rz-alert rz-alert-md rz-variant-flat rz-{status.ClassSuffix()} rz-shade-lighter lc-alert");

        if (!output.Attributes.ContainsName("role"))
            output.Attributes.SetAttribute("role", "alert");

        output.PreContent.AppendHtml(new HtmlString("<div class=\"rz-alert-item\"><div class=\"rz-alert-message\"><div class=\"rz-alert-content\">"));
        output.PostContent.AppendHtml(new HtmlString("</div></div></div>"));
    }
}

/// <summary>A text, password or email input drawn like TextInput; combines with <c>asp-for</c>.</summary>
[HtmlTargetElement("input", Attributes = "lc-input", TagStructure = TagStructure.WithoutEndTag)]
public class InputTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("lc-input");

        StaticModifiers.AddClasses(output, "rz-textbox lc-input");
    }
}
