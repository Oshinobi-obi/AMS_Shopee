// CartState.cs — keeps the cart badge in the top bar in sync with the database.
// Call RefreshAsync() after anything changes the cart; subscribers re-render.

using AMS_Shopee.Services.Requisition;
using Microsoft.AspNetCore.Components.Authorization;

namespace AMS_Shopee.Services.Ui;

public sealed class CartState(RequisitionService requisitions, AuthenticationStateProvider auth)
{
    public int Count { get; private set; }
    public event Action? Changed;

    public async Task RefreshAsync()
    {
        var user = CurrentUser.From((await auth.GetAuthenticationStateAsync()).User);
        Count = await requisitions.GetCartLineCountAsync(user);
        Changed?.Invoke();
    }
}
