class Modal {
    constructor(id) {
        this.ElementId = id;
        // @ts-ignore
        this.Instance = new bootstrap.Modal(`#${this.ElementId}`, {
            keyboard: false
        });
    }
    Show(header, message) {
        document.getElementById(`${this.ElementId}Header`).innerText = header;
        document.getElementById(`${this.ElementId}Message`).innerText = message;
        this.Instance.show();
    }
}
const ErrorModal = new Modal('ErrorModal');
//# sourceMappingURL=Modal.js.map