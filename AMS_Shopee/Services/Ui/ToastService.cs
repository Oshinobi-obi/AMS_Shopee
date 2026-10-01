// ToastService.cs — small, non-blocking "Added to cart" style messages.
// Rendered by <ToastHost /> in MainLayout. Registered Scoped (per circuit).

using AMS_Shopee.Components.Shared.Modals;

namespace AMS_Shopee.Services.Ui;

public sealed record ToastMessage(Guid Id, string Text, ModalVariant Variant);

public sealed class ToastService
{
    private readonly List<ToastMessage> _items = [];
    public IReadOnlyList<ToastMessage> Items => _items;
    public event Action? Changed;

    public void Show(string text, ModalVariant variant = ModalVariant.Success, int milliseconds = 3500)
    {
        var toast = new ToastMessage(Guid.NewGuid(), text, variant);
        _items.Add(toast);
        if (_items.Count > 3) _items.RemoveAt(0);   // never pile up
        Changed?.Invoke();
        _ = RemoveLaterAsync(toast, milliseconds);
    }

    public void Dismiss(Guid id)
    {
        if (_items.RemoveAll(t => t.Id == id) > 0) Changed?.Invoke();
    }

    private async Task RemoveLaterAsync(ToastMessage toast, int ms)
    {
        await Task.Delay(ms);
        if (_items.Remove(toast)) Changed?.Invoke();
    }
}
