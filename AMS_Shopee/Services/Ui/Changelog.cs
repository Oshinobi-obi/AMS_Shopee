// This is the list for the What's new page (/whats-new), newest first.
// When I publish an update, I add a release at the TOP of the list and raise the version.
// The gold dot on "What's new" in the top bar shows for everyone who hasn't opened the
// page since the newest version.

namespace AMS_Shopee.Services.Ui;

public enum ChangeKind { New, Improved, Fixed }

public sealed record Change(ChangeKind Kind, string Text);

public sealed record Release(string Version, DateOnly Date, string Title, IReadOnlyList<Change> Changes);

public static class Changelog
{
    public static string LatestVersion => Releases[0].Version;

    public static readonly IReadOnlyList<Release> Releases =
    [
        new("1.15", new DateOnly(2026, 10, 21), "Choose colors",
        [
            new(ChangeKind.New, "Items that come in colors (ballpens, folders…) let you choose how many of each color. The colors are written on your requisition slip."),
            new(ChangeKind.Improved, "MFD and EXP show only the month and year when that's what the pack says, like \"EXP Mar 2027\"."),
            new(ChangeKind.Improved, "My accountability uses the same calendar as the rest of the site for your birthday."),
        ]),

        new("1.14", new DateOnly(2026, 10, 20), "Your accountability is private",
        [
            new(ChangeKind.New, "My accountability asks for your birthday before showing your documents. The first time, you set it yourself; after that, only AMS can change it."),
            new(ChangeKind.New, "Tick the documents you want and download them together in one ZIP."),
            new(ChangeKind.Improved, "The total value was removed from My accountability."),
        ]),

        new("1.13", new DateOnly(2026, 10, 20), "Equipment on your documents",
        [
            new(ChangeKind.Improved, "My accountability shows what each PAR and ICS covers: Laptop, Printer, Office chair and so on."),
        ]),

        new("1.12", new DateOnly(2026, 10, 20), "Seasonal themes",
        [
            new(ChangeKind.New, "AMS can dress up the top bar and page headers for the season: Halloween, Christmas, Valentine's Day, New Year, Independence Day and Teachers' Month."),
            new(ChangeKind.Fixed, "My accountability: a long position no longer pushes the name box outside the card."),
        ]),

        new("1.11", new DateOnly(2026, 10, 20), "New name: e-SRS",
        [
            new(ChangeKind.Improved, "AMS Supplies is now e-SRS, the Supply Requisition System. AMS's side is now e-PINS, the Property Inventory and Information System. Everything else works the same."),
        ]),

        new("1.10", new DateOnly(2026, 10, 18), "My accountability",
        [
            new(ChangeKind.New, "My accountability page: RO personnel choose their office and their name to see the property under their name and their PAR, ICS and Return Slips. No log-in needed."),
            new(ChangeKind.New, "Open any document to read the PDF, see the property listed on it, and download or print a copy."),
            new(ChangeKind.New, "Total value counts only the property still in your care. Returned property no longer counts."),
            new(ChangeKind.Improved, "Announcements now stay under the top bar while you scroll, until AMS ends them."),
        ]),

        new("1.9", new DateOnly(2026, 10, 18), "Smoother feel",
        [
            new(ChangeKind.Improved, "When you add an item, it flies into the cart and the cart count pops, so you can see it was added."),
            new(ChangeKind.Improved, "Supply cards come in one after another and lift slightly under the mouse. APP-CSE bars grow to their value."),
            new(ChangeKind.Improved, "A check mark confirms that your requisition was submitted, and the status steps of a requisition fill in one by one."),
            new(ChangeKind.Improved, "Pages show grey placeholders while they load instead of a blank screen."),
            new(ChangeKind.Improved, "If you turned off animations on your computer or phone, e-SRS shows none."),
        ]),

        new("1.8", new DateOnly(2026, 10, 13), "Easier scrolling",
        [
            new(ChangeKind.Improved, "The top bar (catalog, My requests, cart and your account) now stays at the top of the screen while you scroll."),
            new(ChangeKind.New, "A Back to top button appears at the bottom right after you scroll down a long page."),
        ]),

        new("1.7", new DateOnly(2026, 10, 11), "Clearer announcements",
        [
            new(ChangeKind.Improved, "Announcements from AMS have a new look: a coloured label (Information, Important or Urgent), who posted it, and when it was posted and until when it runs."),
            new(ChangeKind.Improved, "Announcements now stay on every page for the whole day they run, so no office misses them. They can no longer be closed; they go away by themselves when AMS ends them."),
            new(ChangeKind.Improved, "Long announcements keep their line breaks, so lists and schedules from AMS are easier to read."),
        ]),

        new("1.6", new DateOnly(2026, 10, 11), "Help, expiry dates and cleaner printing",
        [
            new(ChangeKind.New, "Help / FAQ page with answers to common questions about requesting supplies, searchable by keyword."),
            new(ChangeKind.New, "This What's new page, listing every update to e-SRS."),
            new(ChangeKind.New, "Perishable supplies (like printer ink and toner) now show their MFD and EXP on the catalog card and in the item details. They turn amber when expiring within 90 days and red once expired."),
            new(ChangeKind.Improved, "The catalog loads faster: item photos are now much smaller to download."),
            new(ChangeKind.Improved, "The stock shown in the catalog now goes down when AMS releases the items to an office, not when the requisition is approved."),
            new(ChangeKind.Fixed, "Print RIS for signing now prints the whole slip on one A4 page, with no edges cut off."),
            new(ChangeKind.Fixed, "In dark mode, the printed RIS came out with a dark background. Printouts are now always on white paper."),
            new(ChangeKind.Fixed, "The \"Fiscal year\" label on My APP-CSE was hard to read on the blue banner."),
        ]),

        new("1.5", new DateOnly(2026, 10, 6), "RIS matches the AMS form",
        [
            new(ChangeKind.Improved, "The Requisition and Issue Slip now follows the AMS form (Appendix 63): Bookman Old Style lettering, 14 item rows, a ruled Purpose box and signature boxes."),
            new(ChangeKind.Improved, "The RIS shows only your office beside \"Division Office\", on one line."),
            new(ChangeKind.Improved, "Catalog cards are all the same height. Click an item to see its photo full size."),
            new(ChangeKind.Improved, "The office list now matches the APP-CSE workbook (for example ARD, ASD, Finance, GSU, Records and RPSU)."),
        ]),

        new("1.4", new DateOnly(2026, 10, 6), "Announcements and live pages",
        [
            new(ChangeKind.New, "Announcements from AMS appear at the top of every page, with a sound. Close one once you have read it."),
            new(ChangeKind.New, "Request these items again: copy a past requisition back into your cart in one click."),
            new(ChangeKind.New, "My suggestions: follow what AMS decided on the supplies your office suggested."),
            new(ChangeKind.Improved, "Every page now updates by itself when something changes (catalog, stock, APP-CSE, announcements), with no need to refresh."),
            new(ChangeKind.Fixed, "Sounds could be blocked by the browser without warning. A \"Sound is blocked\" button now appears so you can turn them on with one click."),
        ]),

        new("1.3", new DateOnly(2026, 10, 3), "Dark mode, Philippine time and safer sign-in",
        [
            new(ChangeKind.New, "Light and dark mode, with the sun/moon button at the top right. Your choice is remembered."),
            new(ChangeKind.New, "A clock and date in Philippine time (UTC+8) in the top bar."),
            new(ChangeKind.New, "\"Keep me signed in for 7 days\" on the login window, and a button to show the password while typing."),
            new(ChangeKind.Improved, "Sign-in is better protected: an account is locked for 15 minutes after 5 wrong passwords, and changing your password logs out other computers."),
            new(ChangeKind.Improved, "Pages fit phones, tablets and wide screens."),
        ]),

        new("1.2", new DateOnly(2026, 10, 2), "Follow your requisition to the end",
        [
            new(ChangeKind.New, "Choose who will receive the items, then print the RIS for signing once AMS approves."),
            new(ChangeKind.New, "After the items are released, the scanned signed RIS appears on the requisition's page, to view or download as a PDF."),
            new(ChangeKind.New, "Live status: the page updates the moment AMS approves, rejects or issues your requisition, with a sound and an optional notification."),
            new(ChangeKind.New, "Item photos in the catalog."),
            new(ChangeKind.New, "Cancel a requisition yourself while it is still pending approval."),
            new(ChangeKind.Fixed, "A long purpose no longer runs outside the box on the RIS."),
        ]),

        new("1.0", new DateOnly(2026, 10, 2), "e-SRS is live",
        [
            new(ChangeKind.New, "Catalog of common-use office supplies, with search, categories and stock levels."),
            new(ChangeKind.New, "Office accounts: log in to request supplies for your office."),
            new(ChangeKind.New, "Cart that checks your office's APP-CSE balance as you add items."),
            new(ChangeKind.New, "Submit a Requisition and Issue Slip (RIS), with a live preview of the slip while you fill in the cart."),
            new(ChangeKind.New, "My requests: every requisition of your office and its status."),
            new(ChangeKind.New, "My APP-CSE: your office's allocation, what was issued and what is left, by fiscal year."),
            new(ChangeKind.New, "Suggest a supply: tell AMS about items that aren't in the catalog yet."),
        ]),
    ];
}
