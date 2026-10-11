using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Bookkeeping;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Bookkeeping;

/// <summary>
/// Maps each kind of settlement amount to a QuickBooks account and sets the marketplace's deposit account.
/// </summary>
/// <param name="bookkeeping">Bookkeeping use cases.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class MappingModel(IBookkeepingService bookkeeping, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>Marketplace whose deposit account is edited.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Loads the mapping rows and the deposit account.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var view = await bookkeeping.GetMappingViewAsync(Marketplace.MarketplaceId, cancellationToken);
        Input = new InputModel
        {
            DepositAccountName = view.DepositAccountName,
            Rows = [.. view.Rows.Select(r => new MappingRowInput
            {
                AmountType = r.AmountType,
                AmountDescription = r.AmountDescription,
                QuickBooksAccountName = r.QuickBooksAccountName,
                LineCount = r.LineCount,
            })],
        };
    }

    /// <summary>Saves the accounts.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the page on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var mappings = Input.Rows
            .Select(r => new AccountMappingInput(r.AmountType, r.AmountDescription, r.QuickBooksAccountName))
            .ToList();
        var result = await bookkeeping.SaveMappingsAsync(
            Marketplace.MarketplaceId, Input.DepositAccountName, mappings, User.Identity?.Name ?? "unknown", cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = "Account mapping saved.";
        return RedirectToPage();
    }

    /// <summary>Editable fields.</summary>
    public sealed class InputModel
    {
        /// <summary>QuickBooks account the current marketplace's payouts are deposited into; blank clears it.</summary>
        [Display(Name = "Deposit account")]
        [StringLength(BookkeepingService.MaxAccountNameLength)]
        public string? DepositAccountName { get; set; }

        /// <summary>One row per kind of settlement amount.</summary>
        public List<MappingRowInput> Rows { get; set; } = [];
    }

    /// <summary>One kind of settlement amount and its account.</summary>
    public sealed class MappingRowInput
    {
        /// <summary>Amazon's amount type (or transaction type when blank).</summary>
        [StringLength(64)]
        public string AmountType { get; set; } = string.Empty;

        /// <summary>Amazon's amount description; empty when none.</summary>
        [StringLength(128)]
        public string AmountDescription { get; set; } = string.Empty;

        /// <summary>QuickBooks account; blank leaves the amount unmapped.</summary>
        [StringLength(BookkeepingService.MaxAccountNameLength)]
        public string? QuickBooksAccountName { get; set; }

        /// <summary>How many imported lines are of this kind (display only).</summary>
        public int LineCount { get; set; }
    }
}
