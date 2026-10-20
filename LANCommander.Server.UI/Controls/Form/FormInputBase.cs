using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Shared behaviour of every input control: <c>Value</c>/<c>ValueChanged</c> binding, telling the
/// enclosing <see cref="Form"/> when the value changes, and linking up with the enclosing
/// <see cref="FormItem"/> for its label and validation messages. Inputs also work outside a form.
/// </summary>
public abstract class FormInputBase<TValue> : ControlBase, IDisposable
{
    [Parameter] public TValue? Value { get; set; }

    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Set automatically by <c>@bind-Value</c>; identifies the field for validation.</summary>
    [Parameter] public Expression<Func<TValue?>>? ValueExpression { get; set; }

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public string? Placeholder { get; set; }

    [CascadingParameter] protected EditContext? EditContext { get; set; }

    [CascadingParameter] internal FormItem? FormItem { get; set; }

    [CascadingParameter(Name = Form.DisabledCascadeName)] internal bool FormDisabled { get; set; }

    protected FieldIdentifier? Field { get; private set; }

    /// <summary>The id for the underlying input element, so the FormItem's label points at it.</summary>
    protected string? InputId => FormItem?.InputId;

    protected bool IsDisabled => Disabled || FormDisabled;

    protected bool IsInvalid
    {
        get
        {
            if (Field == null || EditContext == null)
                return false;

            return EditContext.GetValidationMessages(Field.Value).Any();
        }
    }

    protected override void OnParametersSet()
    {
        if (ValueExpression != null && Field == null)
        {
            Field = FieldIdentifier.Create(ValueExpression);
            FormItem?.Register(Field.Value);
        }
    }

    /// <summary>Called by derived controls when the user changes the value.</summary>
    protected async Task SetValueAsync(TValue? value)
    {
        if (EqualityComparer<TValue>.Default.Equals(Value, value))
            return;

        Value = value;

        await ValueChanged.InvokeAsync(value);

        if (Field != null)
            EditContext?.NotifyFieldChanged(Field.Value);
    }

    protected string? BuildClass(string baseClass) =>
        new ClassBuilder()
            .Add(baseClass)
            .If(IsInvalid, "lc-input-invalid")
            .Add(Class)
            .Build();

    public virtual void Dispose()
    {
        if (Field != null)
            FormItem?.Unregister(Field.Value);
    }
}
