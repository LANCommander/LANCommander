export class DomHelper
{
    public static Create(): DomHelper
    {
        return new DomHelper();
    }

    public ClickElement(id: string): void
    {
        document.getElementById(id)?.click();
    }
}
