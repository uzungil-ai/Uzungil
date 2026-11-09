using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Services;

namespace Roma.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ImageOptimizeModel : PageModel
    {
        private readonly ImageBatchOptimizerService _optimizer;

        public ImageOptimizeModel(ImageBatchOptimizerService optimizer)
        {
            _optimizer = optimizer;
        }

        [BindProperty]
        public int BatchSize { get; set; } = 50;

        public ImageBatchOptimizerService.BatchResult? Result { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            BatchSize = Math.Clamp(BatchSize, 1, 500);
            Result = await _optimizer.OptimizeUploadsBatchAsync(BatchSize, cancellationToken);
            return Page();
        }
    }
}
