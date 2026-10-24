// These are the questions and answers on the Help / FAQ page (/help).
// Plain text: a blank line starts a new paragraph, and lines starting with "1." or "•"
// are shown as steps. I keep the answers short and in the words the offices use.

namespace AMS_Shopee.Services.Ui;

public sealed record HelpQa(string Id, string Question, string Answer, string Keywords = "");

public sealed record HelpTopic(string Id, string Title, string Icon, IReadOnlyList<HelpQa> Questions);

public static class HelpContent
{
    public static readonly IReadOnlyList<HelpTopic> Topics =
    [
        new("start", "Getting started", "bi-rocket-takeoff",
        [
            new("what-is", "What is e-SRS?",
                "e-SRS is where offices of the Regional Office request common-use office supplies from the Asset Management Section (AMS).\n\n" +
                "You browse the catalog, add what you need to your cart within your office's APP-CSE, and submit a Requisition and Issue Slip (RIS). " +
                "AMS reviews it in e-PINS, and you follow every step here until the items are issued.",
                "about portal shopee system"),
            new("account", "How do I get an account?",
                "Each office has its own account, created by AMS. Ask your office's supply focal person or the Asset Management Section for the username and starting password.\n\n" +
                "Anyone can browse the catalog without logging in, but only office accounts can add to the cart and submit requisitions.",
                "username register sign up login"),
            new("first-login", "Why do I have to change my password the first time I log in?",
                "The starting password is given by AMS, so the system asks you to choose your own before you continue. " +
                "Use at least 8 characters with both letters and numbers, and don't share it outside your office.\n\n" +
                "You can change it again anytime: click your office name at the top right, then Change password.",
                "password change first time"),
            new("locked", "I typed the wrong password several times and now I can't log in.",
                "After 5 wrong passwords in a row, the account is locked for 15 minutes to protect it. Wait 15 minutes, then try again.\n\n" +
                "If you forgot the password, ask AMS to reset it. They will give you a new starting password.",
                "locked lockout forgot reset wrong password 15 minutes"),
            new("stay-signed-in", "How long do I stay logged in?",
                "Normally for one workday (about 9 hours). If you tick \"Keep me signed in for 7 days\" when logging in, you stay logged in on that computer for 7 days.\n\n" +
                "Only tick it on your own office computer, never on a shared or public one. Changing your password logs out every other computer.",
                "session keep me signed in 7 days logout remember"),
        ]),

        new("catalog", "Browsing the catalog", "bi-grid",
        [
            new("find", "How do I find an item?",
                "Type in the search box at the top of the catalog (item name, specification or stock number), or pick a category. " +
                "You can also filter by In stock, Low stock or Out of stock.\n\n" +
                "Click an item's card to see its details and a bigger photo.",
                "search category filter stock number"),
            new("stock-labels", "What do In stock, Low stock and Out of stock mean?",
                "They show how many AMS has in its stockroom right now:\n" +
                "• In stock: enough on hand.\n" +
                "• Low stock: running low; AMS may give you only part of what you ask for.\n" +
                "• Out of stock: none on hand at the moment.\n\n" +
                "You can still request a low-stock item. AMS decides how much it can issue when it approves.",
                "low out available stockroom"),
            new("not-in-catalog", "The item I need isn't in the catalog.",
                "Click Suggest a supply at the bottom of any page and tell AMS what you need and why. " +
                "You can follow what AMS decides in My suggestions (on the My requests page).",
                "suggest missing new item request not listed"),
            new("expiry", "What are the MFD and EXP shown on some items?",
                "Some supplies expire, like printer ink, toner or glue. For those, the card and the details show:\n" +
                "• MFD: manufacturing date (not all packs show one).\n" +
                "• EXP: expiration date.\n\n" +
                "The dates turn amber when the item expires within 90 days, and red once it has expired.",
                "expiration expiry expired manufacturing perishable ink toner"),
        ]),

        new("appcse", "APP-CSE and your balance", "bi-clipboard-data",
        [
            new("what-appcse", "What is the APP-CSE?",
                "The Annual Procurement Plan for Common-Use Supplies and Equipment (APP-CSE) lists how much of each supply your office planned to use for the year. " +
                "AMS uploads it, and it is the limit of what your office can request.\n\n" +
                "See it in My APP-CSE at the top of the page. You can switch the fiscal year and print it.",
                "annual procurement plan allocation quota limit"),
            new("balance", "How is my remaining balance computed?",
                "Remaining = what your APP-CSE allows − what AMS already issued or approved − what is in requisitions still waiting for approval.\n\n" +
                "The bar on each item card shows this. Items in your cart are counted too, so you can't plan more than you have left.",
                "remaining computed quota meter used pending"),
            new("cant-add", "Why can't I add an item to my cart?",
                "The Add to cart button is turned off when:\n" +
                "• the item is not in your office's APP-CSE for this year, or\n" +
                "• your office has used up its balance for that item.\n\n" +
                "The bar on the card tells you which. If your office really needs more, talk to AMS about your APP-CSE.",
                "disabled button greyed not allowed add to cart"),
            new("zero-pending", "My balance went down even though nothing was issued yet.",
                "Requisitions that are still waiting for approval already count against your balance, so two requests can't use the same allowance. " +
                "If a requisition is rejected or you cancel it, its quantities come back to your balance right away.",
                "balance reduced pending reserved"),
        ]),

        new("request", "Making a requisition (RIS)", "bi-cart3",
        [
            new("how-request", "How do I request supplies?",
                "1. Log in with your office account.\n" +
                "2. Add the items you need to your cart and set the quantities.\n" +
                "3. Open Cart, write the purpose, and choose who requested it and who will receive the items.\n" +
                "4. Check the Requisition and Issue Slip shown beside the cart, then click Submit.\n\n" +
                "You'll get an RIS number right away, and the requisition appears in My requests as Pending approval.",
                "steps how to submit order request cart"),
            new("purpose", "What should I write as the purpose?",
                "Say briefly what the supplies are for, for example: \"For the daily operations of the Records Unit for October\" or \"For the regional training on November 5 to 7\". " +
                "AMS uses it when reviewing, and it is printed on the RIS.",
                "purpose reason justification"),
            new("people", "Who should be \"Requested by\" and \"Received by\"?",
                "Both must be personnel of your own office:\n" +
                "• Requested by: the person asking for the supplies (usually the head or the supply focal person).\n" +
                "• Received by: the person who will pick up the items at AMS and sign for them.\n\n" +
                "You can still change Received by after submitting, until AMS issues the items. If a name is missing from the list, ask AMS to add the person.",
                "requested by received by signatory personnel name missing"),
            new("change-after", "Can I change or cancel a requisition after submitting?",
                "While it is Pending approval, you can cancel it from its page in My requests; its quantities go back to your balance. " +
                "To change the items, cancel it and submit a new one. You can use Request these items again to copy it back into your cart.\n\n" +
                "Once AMS has approved it, it can no longer be cancelled here. Contact AMS instead.",
                "cancel edit change modify withdraw"),
            new("request-again", "How do I request the same items again?",
                "Open a past requisition in My requests and click Request these items again. Its items are added to your cart. " +
                "If your balance is lower now, the quantity is reduced to what you can still request, and you'll be told which lines changed.",
                "repeat reorder copy same items"),
        ]),

        new("after", "After you submit", "bi-receipt",
        [
            new("statuses", "What do the statuses mean?",
                "• Pending approval: AMS hasn't reviewed it yet.\n" +
                "• Approved, ready for release: AMS approved it. Print the RIS, sign it and pick up the items.\n" +
                "• Issued: the items were released to your office. The signed RIS is attached.\n" +
                "• Rejected: AMS didn't approve it; the reason is shown.\n" +
                "• Cancelled: your office cancelled it.",
                "status pending approved issued rejected cancelled meaning"),
            new("know-approved", "How will I know when AMS acts on my requisition?",
                "The page updates by itself, with no need to refresh. You'll hear a sound and see a message when a requisition is approved, rejected or issued.\n\n" +
                "If you allow notifications when the site asks, you'll also get a notification while the tab is in the background. " +
                "If your browser blocks sounds, click the \"Sound is blocked\" button once to turn them on.",
                "notification sound alert update refresh"),
            new("partial", "Why did I get less than I asked for?",
                "When AMS approves, it sets how many it can issue for each line, based on what is in the stockroom. " +
                "Lines issued partially or not at all show a remark on the RIS, and the rest of the quantity goes back to your APP-CSE balance.",
                "partial less fewer quantity reduced not available"),
            new("rejected", "My requisition was rejected. What now?",
                "Open it in My requests to read AMS's reason. Nothing was deducted, so fix what AMS asked for and submit a new requisition. " +
                "Request these items again copies the items back into your cart.",
                "rejected reason resubmit"),
        ]),

        new("pickup", "Printing, signing and pickup", "bi-printer",
        [
            new("print", "How do I print the RIS?",
                "When your requisition is Approved, ready for release, open it in My requests, make sure Received by is filled in, then click Print RIS for signing. " +
                "Only the slip is printed, on one A4 page.\n\n" +
                "In the print window, leave Margins and Scale on Default and turn off \"Headers and footers\". To keep a copy, choose \"Save as PDF\" as the printer.",
                "print a4 paper printer pdf save"),
            new("print-problems", "The printout is cut off or has a dark background.",
                "Use the Print RIS for signing button rather than the browser's own print (Ctrl+P); it always prints the slip in full on white paper, even in dark mode. " +
                "Also check that the paper size in the print window is A4 and Scale is Default.",
                "cut off cropped dark background print problem"),
            new("pickup", "What do I bring when picking up the items?",
                "Bring the printed RIS signed by your Requested by and by your Received by. AMS signs it, hands over the items, scans the signed slip and marks the requisition as Issued.",
                "pickup claim release bring signed"),
            new("signed-copy", "Where can I get the signed RIS afterwards?",
                "Once AMS marks the requisition as Issued, the scanned signed RIS appears on its page in My requests. You can view it there or download it as a PDF anytime.",
                "signed copy pdf download scanned proof"),
        ]),

        new("accountability", "My accountability (property)", "bi-person-vcard",
        [
            new("acc-what", "What is the My accountability page?",
                "It shows the property under your name: every Property Acknowledgement Receipt (PAR), Inventory Custodian Slip (ICS) and Return Slip that AMS filed for you, with the property listed on each.\n\n" +
                "Open it from My accountability at the top of the page. You don't need to log in: choose your office, then your name.",
                "accountability property par ics return slip ro personnel"),
            new("acc-view", "How do I see, download or print my PAR or ICS?",
                "Click a document to open it. The PDF appears with its details and the property on it. Click Download to save a copy, or Print to print it.\n\n" +
                "On a phone, tap Open the PDF; it opens in your phone's PDF viewer, where you can save or share it.",
                "view download print pdf copy"),
            new("acc-missing", "My name or a document isn't there.",
                "Your name appears once AMS adds you as RO personnel of your office. Documents appear once AMS uploads the signed PAR, ICS or Return Slip in e-PINS. " +
                "If something is missing or wrong, contact the Asset Management Section.",
                "missing name not listed wrong document"),
            new("acc-returned", "What does Returned mean?",
                "The property on that PAR or ICS was returned to AMS, and AMS filed the signed Return Slip. It is no longer counted as in your care.",
                "returned return slip in your care"),
            new("acc-value", "How is the total value counted?",
                "It adds up the property in the AMS property register that is under your name and still in your care: quantity × unit cost of each item. " +
                "When a PAR or ICS is returned, its property changes to Returned in the register and no longer counts.\n\n" +
                "A document shows no value until AMS links the register items written on it.",
                "total value amount cost worth computed items count"),
        ]),

        new("other", "Other questions", "bi-three-dots",
        [
            new("announcements", "Where do announcements from AMS appear?",
                "At the top of every page, under the blue bar. Each one shows how important it is (Information, Important or Urgent, in red), when it was posted and until when it runs.\n\n" +
                "Announcements can't be closed: they stay on every page until their end date, or until AMS takes them down. A new announcement shows up by itself, with a sound.",
                "announcement notice message bar"),
            new("dark-mode", "How do I switch to dark mode?",
                "Click the sun/moon button at the top right. Your choice is remembered on that computer. Printouts always come out on white paper.",
                "dark light theme mode night"),
            new("phone", "Can I use e-SRS on my phone?",
                "Yes. The site works on phones and tablets. You can browse, request, follow your requisitions and view the signed RIS. For printing the RIS, a computer is easier.",
                "mobile phone tablet cellphone"),
            new("whats-new", "How do I know what changed in the system?",
                "Click What's new at the top of the page. It lists every update, from the first version to the latest fixes. A gold dot appears on it when there is something you haven't seen yet.",
                "changelog updates new features version"),
            new("contact", "My question isn't here.",
                "Contact the Asset Management Section (AMS) of the Regional Office. For a supply that isn't in the catalog, use Suggest a supply at the bottom of any page.",
                "contact help support ams"),
        ]),
    ];
}
