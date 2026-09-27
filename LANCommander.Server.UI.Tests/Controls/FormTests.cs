using System.ComponentModel.DataAnnotations;
using Bunit;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace LANCommander.Server.UI.Tests.Controls;

public class FormTests : ControlsTestContext
{
    public enum Engine
    {
        Local,
        [Display(Name = "Docker Container")]
        Docker,
    }

    public class GameModel
    {
        public string? Title { get; set; }

        [StringLength(5, ErrorMessage = "Key must be at most 5 characters")]
        public string? Key { get; set; }

        public int Players { get; set; }

        public bool Singleplayer { get; set; }

        public Engine Engine { get; set; }

        public Guid GenreId { get; set; }

        public IEnumerable<Guid> TagIds { get; set; } = [];
    }

    /// <summary>A form with the common inputs, built in C# since the test project has no Razor compiler.</summary>
    private sealed class GameForm : ComponentBase
    {
        [Parameter] public GameModel Model { get; set; } = new();

        [Parameter] public EventCallback OnSubmit { get; set; }

        public Form? Form { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Form>(0);
            builder.AddComponentParameter(1, nameof(LANCommander.Server.UI.Controls.Form.Model), Model);
            builder.AddComponentParameter(2, nameof(LANCommander.Server.UI.Controls.Form.OnSubmit), OnSubmit);
            builder.AddComponentParameter(4, nameof(LANCommander.Server.UI.Controls.Form.ChildContent), (RenderFragment)(content =>
            {
                Item(content, 10, "Title", required: true, inner => Bind<TextInput, string?>(inner, () => Model.Title, v => Model.Title = v));
                Item(content, 20, "Key", required: false, inner => Bind<TextInput, string?>(inner, () => Model.Key, v => Model.Key = v));
                Item(content, 30, "Players", required: false, inner => Bind<NumberInput<int>, int>(inner, () => Model.Players, v => Model.Players = v));
                Item(content, 40, "Singleplayer", required: false, inner => Bind<Switch, bool>(inner, () => Model.Singleplayer, v => Model.Singleplayer = v));
                Item(content, 50, "Engine", required: false, inner => Bind<EnumSelect<Engine>, Engine>(inner, () => Model.Engine, v => Model.Engine = v));

                content.OpenElement(90, "button");
                content.AddAttribute(91, "type", "submit");
                content.AddContent(92, "Save");
                content.CloseElement();
            }));
            builder.AddComponentReferenceCapture(5, form => Form = (Form)form);
            builder.CloseComponent();
        }

        private void Item(RenderTreeBuilder builder, int sequence, string label, bool required, RenderFragment content)
        {
            builder.OpenComponent<FormItem>(sequence);
            builder.AddComponentParameter(sequence + 1, nameof(FormItem.Label), label);
            builder.AddComponentParameter(sequence + 2, nameof(FormItem.Required), required);
            builder.AddComponentParameter(sequence + 3, nameof(FormItem.ChildContent), content);
            builder.CloseComponent();
        }

        private void Bind<TInput, TValue>(RenderTreeBuilder builder, System.Linq.Expressions.Expression<Func<TValue>> expression, Action<TValue> set)
            where TInput : IComponent
        {
            builder.OpenComponent<TInput>(0);
            builder.AddComponentParameter(1, "Value", expression.Compile()());
            builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<TValue>(this, set));
            builder.AddComponentParameter(3, "ValueExpression", expression);
            builder.CloseComponent();
        }
    }

    [Fact]
    public void TextInput_Change_UpdatesValue()
    {
        string? value = null;

        var input = Render<TextInput>(p => p.Add(x => x.ValueChanged, v => value = v));

        input.Find("input").Change("Arena Blitz");

        Assert.Equal("Arena Blitz", value);
    }

    [Fact]
    public void TextInput_OnEnter_Fires()
    {
        var pressed = false;

        var input = Render<TextInput>(p => p.Add(x => x.OnEnter, () => pressed = true));

        input.Find("input").KeyDown("Enter");

        Assert.True(pressed);
    }

    [Fact]
    public void TextInput_Icon_RendersAffix()
    {
        var input = Render<TextInput>(p => p.Add(x => x.Icon, IconType.MagnifyingGlass));

        Assert.NotNull(input.Find(".lc-input-affix svg.lc-input-affix-icon"));
    }

    [Fact]
    public void FormItem_LabelPointsAtInput()
    {
        var form = Render<GameForm>();

        var label = form.FindAll("label.lc-form-item-label").First(l => l.TextContent.Contains("Title"));
        var input = form.Find($"#{label.GetAttribute("for")}");

        Assert.Equal("input", input.TagName.ToLowerInvariant());
    }

    [Fact]
    public void FormItem_Required_ShowsAsterisk()
    {
        var form = Render<GameForm>();

        var title = form.FindAll(".lc-form-item").First(i => i.TextContent.Contains("Title"));

        Assert.NotNull(title.QuerySelector(".lc-form-item-required"));
    }

    [Fact]
    public void Submit_WithMissingRequiredField_ShowsMessage_AndDoesNotSubmit()
    {
        var submitted = false;
        var form = Render<GameForm>(p => p.Add(x => x.OnSubmit, () => submitted = true));

        form.Find("form").Submit();

        Assert.False(submitted);
        form.WaitForAssertion(() => Assert.Contains("Title is required", form.Find(".lc-form-item-has-error .lc-form-item-error").TextContent));
    }

    [Fact]
    public void Submit_WithDataAnnotationError_ShowsMessage()
    {
        var model = new GameModel { Title = "Arena Blitz", Key = "TOO-LONG" };
        var form = Render<GameForm>(p => p.Add(x => x.Model, model));

        form.Find("form").Submit();

        form.WaitForAssertion(() => Assert.Contains("Key must be at most 5 characters", form.Markup));
    }

    [Fact]
    public void Submit_WhenValid_CallsOnSubmit()
    {
        var submitted = false;
        var model = new GameModel { Title = "Arena Blitz" };
        var form = Render<GameForm>(p => p.Add(x => x.Model, model).Add(x => x.OnSubmit, () => submitted = true));

        form.Find("form").Submit();

        Assert.True(submitted);
        Assert.Empty(form.FindAll(".lc-form-item-error"));
    }

    [Fact]
    public void FixingAField_ClearsItsMessage()
    {
        var model = new GameModel();
        var form = Render<GameForm>(p => p.Add(x => x.Model, model));

        form.Find("form").Submit();
        form.WaitForAssertion(() => Assert.NotEmpty(form.FindAll(".lc-form-item-error")));

        var label = form.FindAll("label.lc-form-item-label").First(l => l.TextContent.Contains("Title"));
        form.Find($"#{label.GetAttribute("for")}").Change("Arena Blitz");

        form.WaitForAssertion(() => Assert.Empty(form.FindAll(".lc-form-item-error")));
        Assert.Equal("Arena Blitz", model.Title);
    }

    [Fact]
    public void IsModified_TracksEditsUntilMarkedUnmodified()
    {
        var model = new GameModel { Title = "Arena Blitz" };
        var form = Render<GameForm>(p => p.Add(x => x.Model, model));

        Assert.False(form.Instance.Form!.IsModified);

        var label = form.FindAll("label.lc-form-item-label").First(l => l.TextContent.Contains("Title"));
        form.Find($"#{label.GetAttribute("for")}").Change("Arena Blitz 2");

        Assert.True(form.Instance.Form!.IsModified);

        form.InvokeAsync(() => form.Instance.Form!.MarkAsUnmodified());

        Assert.False(form.Instance.Form!.IsModified);
    }

    [Fact]
    public void Validate_ReturnsWhetherValid()
    {
        var model = new GameModel();
        var form = Render<GameForm>(p => p.Add(x => x.Model, model));

        Assert.False(form.InvokeAsync(() => form.Instance.Form!.Validate()).Result);

        model.Title = "Arena Blitz";

        Assert.True(form.InvokeAsync(() => form.Instance.Form!.Validate()).Result);
    }

    [Fact]
    public void Switch_Click_Toggles()
    {
        var value = false;
        var toggle = Render<Switch>(p => p.Add(x => x.ValueChanged, v => value = v).AddChildContent("Enabled"));

        toggle.Find(".rz-switch").Click();

        Assert.True(value);
        Assert.Equal("Enabled", toggle.Find("label.lc-switch-label").TextContent);
    }

    [Fact]
    public void Checkbox_Click_Toggles()
    {
        var value = false;
        var checkbox = Render<Checkbox>(p => p.Add(x => x.ValueChanged, v => value = v).AddChildContent("Singleplayer"));

        checkbox.Find(".rz-chkbox-box").Click();

        Assert.True(value);
    }

    [Fact]
    public void EnumSelect_UsesDisplayNames()
    {
        Assert.Equal("Docker Container", EnumSelect<Engine>.LabelFor(Engine.Docker));
        Assert.Equal("Local", EnumSelect<Engine>.LabelFor(Engine.Local));
    }

    [Fact]
    public void Select_ChoosingAnItem_UpdatesValue()
    {
        var chosen = Guid.Empty;
        var action = Guid.NewGuid();

        var select = Render<Select<Guid>>(p => p
            .Add(x => x.Items, new[] { new SelectItem<Guid>(action, "Action"), new SelectItem<Guid>(Guid.NewGuid(), "Racing") })
            .Add(x => x.ValueChanged, v => chosen = v));

        select.FindAll("li").First(li => li.TextContent.Contains("Action")).Click();

        Assert.Equal(action, chosen);
    }

    [Fact]
    public void Select_CombinesItemsAndOptionChildren()
    {
        var select = Render<Select<int>>(p => p
            .Add(x => x.Items, new[] { new SelectItem<int>(1, "One") })
            .AddChildContent<SelectOption<int>>(o => o.Add(x => x.Value, 2).Add(x => x.Label, "Two")));

        var labels = select.FindAll("li").Select(li => li.TextContent.Trim()).ToList();

        Assert.Contains("One", labels);
        Assert.Contains("Two", labels);
    }

    [Fact]
    public void ToSelectItems_ProjectsValuesAndLabels()
    {
        var items = new[] { (Id: 1, Name: "Arena Blitz") }.ToSelectItems(g => g.Id, g => g.Name).ToList();

        Assert.Equal(new SelectItem<int>(1, "Arena Blitz"), Assert.Single(items));
    }
}
