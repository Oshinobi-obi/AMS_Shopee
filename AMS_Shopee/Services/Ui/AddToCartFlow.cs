// AddToCartFlow.cs — the one place that decides what "Add to cart" does:
//   guest            → login modal (and the item is added right after login)
//   blocked by rules → explanation modal (e.g. "Exceeds APP-CSE balance")
//   allowed          → toast, or a warning modal when warehouse stock is short
// Used by item cards, the item detail modal, and the ?add= resume after login.

using AMS_Shopee.Components.Account;
using AMS_Shopee.Components.Shared.Modals;
using AMS_Shopee.Services.Requisition;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace AMS_Shopee.Services.Ui;

public sealed class AddToCartFlow(
    RequisitionService requisitions,
    ModalService modal,
    ToastService toast,
    CartState cart,
    AuthenticationStateProvider auth,
    NavigationManager nav,
    ILogger<AddToCartFlow> log)
{
    public async Task<CurrentUser?> GetUserAsync() =>
        CurrentUser.From((await auth.GetAuthenticationStateAsync()).User);

    public Task ShowLoginAsync() => ShowLoginAsync(null, 1, null);

    public Task ShowLoginAsync(uint? pendingItemId, int pendingQty, string? message) =>
        modal.ShowComponentAsync<LoginForm>(
            pendingItemId is null ? "Log in" : "Log in to add this item",
            new()
            {
                ["ReturnUrl"] = new Uri(nav.Uri).AbsolutePath,   // drop ?login= / ?add= leftovers
                ["PendingItemId"] = pendingItemId,
                ["PendingQty"] = pendingQty,
                ["Message"] = message,
            },
            ModalSize.Small);

    /// Returns true when the item actually went into the cart.
    public async Task<bool> AddAsync(uint itemId, string itemName, string unit, int qty)
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            await ShowLoginAsync(itemId, qty, null);
            return false;
        }

        CartCheck result;
        try
        {
            result = await requisitions.AddToCartAsync(user, itemId, qty);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Add to cart failed for item {ItemId}", itemId);
            await modal.ErrorAsync("Couldn't add the item",
                "Your cart wasn't changed because of a connection problem. Please try again.");
            return false;
        }

        if (!result.Allowed)
        {
            var problem = result.Issues[0];
            await modal.WarningAsync(problem.Title, problem.Message);
            return false;
        }

        await cart.RefreshAsync();

        var warning = result.Issues.FirstOrDefault(i => i.Severity == IssueSeverity.Warning);
        if (warning is null)
            toast.Show($"Added {qty} {unit} of {itemName} to your cart.");
        else
            await modal.AlertAsync("Added, but warehouse stock is limited",
                $"{qty} {unit} of {itemName} is in your cart. {warning.Message}", ModalVariant.Warning);
        return true;
    }
}
