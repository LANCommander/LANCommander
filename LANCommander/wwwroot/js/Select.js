class Select {
    Element;
    Options;

    constructor(selector, options) {
        this.Options = options;
        this.Element = $(selector).get(0);

        this.Initialize();
    }

    Initialize() {
        let selected = [];

        for (let option of this.Options) {
            if (option.selected)
                selected.push(option.value);
        }

        $(this.Element).val(selected.join('|'));

        $(this.Element).selectize({
            delimiter: '|',
            plugins: ['remove_button'],
            create: true,
            valueField: 'value',
            labelField: 'text',
            searchField: 'text',
            options: this.Options,
            onItemAdd: function (value) {
                for (let option of Object.keys(this.options)) {
                    if (option.value == value) {
                        this.$input.siblings('select').append(`<option value="${option.value}" selected>${option.text}</option>`);
                    }
                }
            },
            onItemRemove: function (value) {
                for (let option of Object.keys(this.options)) {
                    if (option.value == value) {
                        this.$input.siblings('select').find(`option[value="${option.value}"]`).remove();
                    }
                }
            },
            onInitialize: function () {
                for (let option of Object.keys(this.options)) {
                    if (this.options[option].selected) {
                        this.$input.siblings('select').append(`<option value="${this.options[option].value}" selected>${this.options[option].text}</option>`);
                    }
                }
            }
        });
    }
}