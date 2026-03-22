using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Serilog;
using FluxAnswer.Models;
using FluxAnswer.Repositories;

namespace FluxAnswer.Services.Scraping.TikTok
{
    /// <summary>
    /// Extracts TikTok comments using Playwright headless Chromium.
    /// Replaces the yt-dlp approach which requires cookies.
    /// </summary>
    public class PlaywrightCommentsExtractionService : ICommentsExtractionService
    {
        private readonly IExtractedCommentRepo? _commentRepo;
        private readonly int _timeoutMs;

        public PlaywrightCommentsExtractionService(
            IExtractedCommentRepo? commentRepo = null,
            int timeoutSeconds = 30)
        {
            _commentRepo = commentRepo;
            _timeoutMs = timeoutSeconds * 1000;
            Log.Information("[Comments][Init] PlaywrightCommentsExtractionService created. Repo={RepoAvailable}, Timeout={TimeoutMs}ms",
                commentRepo != null, _timeoutMs);
        }

        public async Task<List<CommentData>> ExtractCommentsAsync(string videoUrl, int limit = 12)
        {
            var comments = new List<CommentData>();
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[Comments][Start] Extracting comments from: {Url} (limit={Limit})", videoUrl, limit);

                Log.Debug("[Comments][Step 1/7] Creating Playwright instance...");
                using var playwright = await Playwright.CreateAsync();
                Log.Debug("[Comments][Step 1/7] Playwright created in {Ms}ms", sw.ElapsedMilliseconds);

                Log.Debug("[Comments][Step 2/7] Launching Chromium headless...");
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--disable-blink-features=AutomationControlled" }
                });
                Log.Debug("[Comments][Step 2/7] Chromium launched in {Ms}ms", sw.ElapsedMilliseconds);

                Log.Debug("[Comments][Step 3/7] Creating browser context...");
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                    Locale = "en-US"
                });

                var page = await context.NewPageAsync();
                page.SetDefaultTimeout(_timeoutMs);
                Log.Debug("[Comments][Step 3/7] Context + page ready in {Ms}ms", sw.ElapsedMilliseconds);

                // Navigate to video
                Log.Information("[Comments][Step 4/7] Navigating to {Url}...", videoUrl);
                var navSw = Stopwatch.StartNew();
                await page.GotoAsync(videoUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = _timeoutMs
                });
                Log.Information("[Comments][Step 4/7] Page loaded in {Ms}ms. Current URL: {CurrentUrl}",
                    navSw.ElapsedMilliseconds, page.Url);

                // Click comment icon to open panel (use .first to avoid strict mode with multiple matches)
                Log.Debug("[Comments][Step 5/7] Looking for comment icon [data-e2e='comment-icon']...");
                var commentIcon = page.Locator("span[data-e2e='comment-icon']").First;
                try
                {
                    await commentIcon.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
                    await commentIcon.ClickAsync();
                    Log.Information("[Comments][Step 5/7] Comment icon found and clicked");
                }
                catch (TimeoutException)
                {
                    Log.Warning("[Comments][Step 5/7] Comment icon not found after 10s — comments may be inline or page structure changed");
                }

                // Wait for REAL comments to load (not skeleton placeholders)
                Log.Debug("[Comments][Step 6/7] Waiting for real comments to load (not skeletons)...");
                var realCommentSelector = "span[data-e2e='comment-level-1']";
                try
                {
                    await page.WaitForSelectorAsync(realCommentSelector, new PageWaitForSelectorOptions
                    {
                        Timeout = 15000,
                        State = WaitForSelectorState.Attached
                    });
                    Log.Debug("[Comments][Step 6/7] Real comments loaded (found {Selector})", realCommentSelector);
                }
                catch (TimeoutException)
                {
                    var fallbackSelector = "div[data-e2e='comment-username-1']";
                    try
                    {
                        await page.WaitForSelectorAsync(fallbackSelector, new PageWaitForSelectorOptions
                        {
                            Timeout = 5000,
                            State = WaitForSelectorState.Attached
                        });
                        Log.Debug("[Comments][Step 6/7] Real comments loaded via fallback (found {Selector})", fallbackSelector);
                    }
                    catch (TimeoutException)
                    {
                        var pageTitle = await page.TitleAsync();
                        var bodySnippet = await page.EvaluateAsync<string>("() => document.body?.innerText?.substring(0, 500) || 'EMPTY'");
                        Log.Error("[Comments][Step 6/7] NO real comments loaded. PageTitle='{Title}', BodySnippet='{Snippet}'",
                            pageTitle, bodySnippet);
                        return comments;
                    }
                }

                // Extra wait for remaining comments to render
                await page.WaitForTimeoutAsync(2000);

                // Extract comments from DOM
                // Debug: dump first real comment wrapper
                var debugHtml = await page.EvaluateAsync<string>(@"() => {
                    const textEl = document.querySelector('span[data-e2e=""comment-level-1""]');
                    if (!textEl) return 'NO_COMMENT_TEXT_FOUND';
                    // Walk up to find the wrapper
                    let wrapper = textEl.closest('div[class*=""DivCommentObjectWrapper""]') || textEl.parentElement?.parentElement?.parentElement;
                    return wrapper ? wrapper.innerHTML.substring(0, 2000) : 'TEXT_FOUND_NO_WRAPPER: ' + textEl.outerHTML.substring(0, 500);
                }");
                Log.Information("[Comments][Debug] First real comment wrapper: {Html}", debugHtml);

                // Debug: read comment text directly
                var debugText = await page.EvaluateAsync<string>(@"() => {
                    const textEl = document.querySelector('span[data-e2e=""comment-level-1""]');
                    if (!textEl) return 'NO_TEXT_ELEMENT';
                    return 'innerText=[' + textEl.innerText + '] textContent=[' + textEl.textContent + '] childNodes=' + textEl.childNodes.length + ' innerHTML=' + textEl.innerHTML.substring(0, 500);
                }");
                Log.Information("[Comments][Debug] Comment text analysis: {TextDebug}", debugText);

                // Debug: count how many real comment texts exist
                var commentCount = await page.EvaluateAsync<int>(@"() => document.querySelectorAll('span[data-e2e=""comment-level-1""]').length");
                Log.Information("[Comments][Debug] Total span[data-e2e='comment-level-1'] found: {Count}", commentCount);

                Log.Information("[Comments][Step 7/7] Extracting comments from DOM using data-e2e selectors...");
                comments = await ExtractCommentsFromDomAsync(page, limit);

                Log.Information("[Comments][Done] Extracted {Count} comments from {Url} in {Ms}ms",
                    comments.Count, videoUrl, sw.ElapsedMilliseconds);

                // Log each comment for debugging
                for (int i = 0; i < comments.Count; i++)
                {
                    var c = comments[i];
                    Log.Debug("[Comments][Result {Index}] Author='{Author}', Likes={Likes}, Text='{Text}'",
                        i, c.Author, c.LikeCount, c.Text.Length > 80 ? c.Text.Substring(0, 80) + "..." : c.Text);
                }

                // Persist to DB if repo available
                if (_commentRepo != null && comments.Count > 0)
                {
                    Log.Information("[Comments][Persist] Saving {Count} comments to extract_comments table...", comments.Count);
                    await PersistCommentsAsync(videoUrl, comments);
                }
                else if (_commentRepo == null)
                {
                    Log.Warning("[Comments][Persist] CommentRepo is null — comments will NOT be persisted");
                }

                return comments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Comments][FATAL] Failed to extract comments from {Url} after {Ms}ms. Error: {Error}",
                    videoUrl, sw.ElapsedMilliseconds, ex.Message);
                return comments;
            }
        }

        private async Task<List<CommentData>> ExtractCommentsFromDomAsync(IPage page, int limit)
        {
            var results = new List<CommentData>();

            var jsExtract = @"(limit) => {
                const comments = [];
                // Find all comment text elements directly
                const textEls = document.querySelectorAll('span[data-e2e=""comment-level-1""]');

                for (let i = 0; i < Math.min(textEls.length, limit); i++) {
                    const textEl = textEls[i];
                    const text = textEl.innerText.trim();
                    if (!text) continue;

                    // Walk up to find the comment container
                    let container = textEl.closest('div[class*=""DivCommentObjectWrapper""]')
                                 || textEl.closest('div[class*=""CommentItemWrapper""]')
                                 || textEl.parentElement?.parentElement?.parentElement;

                    // Username
                    let author = '';
                    if (container) {
                        const userEl = container.querySelector('div[data-e2e=""comment-username-1""] a p')
                                    || container.querySelector('div[data-e2e=""comment-username-1""] a')
                                    || container.querySelector('[data-e2e=""comment-username-1""]');
                        author = userEl ? userEl.innerText.trim() : '';
                    }

                    // Likes
                    let likeCount = 0;
                    if (container) {
                        const likeEl = container.querySelector('div[class*=""DivLikeWrapper""] span')
                                    || container.querySelector('div[class*=""LikeContainer""] span')
                                    || container.querySelector('span[data-e2e=""comment-like-count""]');
                        if (likeEl) {
                            const likeText = likeEl.innerText.trim();
                            if (likeText.endsWith('K')) likeCount = Math.round(parseFloat(likeText) * 1000);
                            else if (likeText.endsWith('M')) likeCount = Math.round(parseFloat(likeText) * 1000000);
                            else likeCount = parseInt(likeText) || 0;
                        }
                    }

                    comments.push({ text, author, likeCount, index: i });
                }
                return comments;
            }";

            Log.Debug("[Comments][DOM] Running JavaScript extraction (limit={Limit})...", limit);
            var extracted = await page.EvaluateAsync<List<JsComment>>(jsExtract, limit);

            var rawCount = extracted?.Count ?? 0;
            Log.Information("[Comments][DOM] JavaScript returned {RawCount} raw comments", rawCount);

            if (extracted != null)
            {
                foreach (var c in extracted)
                {
                    results.Add(new CommentData
                    {
                        CommentId = $"pw_{c.Index}",
                        Text = c.Text,
                        Author = c.Author,
                        LikeCount = c.LikeCount
                    });
                }
            }

            return results;
        }

        private async Task PersistCommentsAsync(string videoUrl, List<CommentData> comments)
        {
            var saved = 0;
            var failed = 0;

            foreach (var c in comments)
            {
                try
                {
                    var record = new ExtractedCommentRecord
                    {
                        VideoId = videoUrl,
                        CommentExternalId = c.CommentId,
                        Text = c.Text,
                        Author = c.Author,
                        LikeCount = c.LikeCount ?? 0,
                        CommentedAt = c.Timestamp.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(c.Timestamp.Value).DateTime
                            : null
                    };
                    await _commentRepo!.CreateAsync(record);
                    saved++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Warning(ex, "[Comments][Persist] Failed to save comment by '{Author}': {Error}",
                        c.Author, ex.Message);
                }
            }

            Log.Information("[Comments][Persist] Result: {Saved} saved, {Failed} failed out of {Total}",
                saved, failed, comments.Count);
        }

        /// <summary>DTO for JavaScript evaluation result.</summary>
        private class JsComment
        {
            public string Text { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public int LikeCount { get; set; }
            public int Index { get; set; }
        }
    }
}
