using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluxAnswer.Configuration;
using FluxAnswer.Models;
using FluxAnswer.Repositories;
using FluxAnswer.Services.Api;
using Serilog;

namespace FluxAnswer.Services.Pipeline
{
    public class BotAccountVideoStageService : IBotAccountVideoStageService
    {
        private readonly IVideoRepo _videoRepo;
        private readonly IResponseGenerationService _responseGenerationService;
        private readonly IBotAccountRepo _botAccountRepo;
        private readonly IBotAccountVideoRepo _botAccountVideoRepo;
        private readonly IDefaultCommentRepo _defaultCommentRepo;
        private readonly IConfigurationManager _config;
        private static readonly Random _rng = new();

        public BotAccountVideoStageService(
            IVideoRepo videoRepo,
            IResponseGenerationService responseGenerationService,
            IBotAccountRepo botAccountRepo,
            IBotAccountVideoRepo botAccountVideoRepo,
            IDefaultCommentRepo defaultCommentRepo,
            IConfigurationManager config)
        {
            _videoRepo = videoRepo;
            _responseGenerationService = responseGenerationService;
            _botAccountRepo = botAccountRepo;
            _botAccountVideoRepo = botAccountVideoRepo;
            _defaultCommentRepo = defaultCommentRepo;
            _config = config;
        }

        public async Task ProcessAsync(VideoRecord video)
        {
            if (!video.ResponseGenerated || string.IsNullOrWhiteSpace(video.ResponseText))
            {
                Log.Warning("[WAIT] BotAccountVideo stage skipped for {VideoId}: response_text is not ready", video.TiktokVideoId);
                video.CustomCommentsSuccess = false;
                video.CustomCommentsGeneratedCount = 0;
                await UpdateVideoAsync(video);
                return;
            }

            Log.Information("Generating BotAccountVideo records for {VideoId} (statusCode={StatusCode})",
                video.TiktokVideoId, video.StatusCode);
            video.SetStatus(VideoStatus.Processing);
            await UpdateVideoAsync(video);

            if (video.StatusCode == 1)
            {
                // Ruta API: personalizar vía modify-comment
                var result = await _responseGenerationService.GenerateCustomCommentsForBotAccountsAsync(
                    video, _config.CustomCommentsPerBotAccount);

                video.CustomCommentsSuccess = result.Success;
                video.CustomCommentsGeneratedCount = result.GeneratedCount;
            }
            else
            {
                // Ruta por defecto: asignar frases aleatorias de default_comments
                var result = await AssignDefaultCommentsAsync(video);
                video.CustomCommentsSuccess = result.Success;
                video.CustomCommentsGeneratedCount = result.GeneratedCount;
            }

            await UpdateVideoAsync(video);

            if (!video.CustomCommentsSuccess)
                Log.Warning("[WARN] BotAccountVideo stage completed with errors for {VideoId}", video.TiktokVideoId);
            else
                Log.Information("[OK] BotAccountVideo stage completed for {VideoId}. Records: {Count}",
                    video.TiktokVideoId, video.CustomCommentsGeneratedCount);
        }

        /// <summary>
        /// Asigna frases por defecto aleatorias de la tabla default_comments
        /// a cada bot account activa, creando registros en bot_account_video.
        /// </summary>
        private async Task<(bool Success, int GeneratedCount)> AssignDefaultCommentsAsync(VideoRecord video)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
            {
                Log.Warning("Cannot assign default comments: video.Id is empty");
                return (false, 0);
            }

            var defaultComments = await _defaultCommentRepo.GetAllAsync();
            if (defaultComments.Count == 0)
            {
                Log.Warning("No default comments found in database. Skipping for {VideoId}", video.TiktokVideoId);
                return (false, 0);
            }

            var accounts = await _botAccountRepo.GetActiveAccountsAsync();
            if (accounts.Count == 0)
            {
                Log.Information("No active bot accounts. Skipping default comments for {VideoId}", video.TiktokVideoId);
                return (true, 0);
            }

            var totalCreated = 0;

            foreach (var account in accounts)
            {
                if (string.IsNullOrWhiteSpace(account.Id)) continue;

                // Verificar si ya tiene registros para este video
                var existing = await _botAccountVideoRepo.GetByBotAccountIdAsync(account.Id);
                var existingForVideo = existing
                    .Where(link => string.Equals(link.VideoId, video.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (existingForVideo.Count >= _config.CustomCommentsPerBotAccount)
                {
                    Log.Information("Account {Username} already has {Count} comments for {VideoId}, skipping",
                        account.Username, existingForVideo.Count, video.TiktokVideoId);
                    continue;
                }

                var missing = _config.CustomCommentsPerBotAccount - existingForVideo.Count;
                var usedIndices = new HashSet<int>();

                for (var i = 0; i < missing; i++)
                {
                    // Elegir frase aleatoria sin repetir dentro de la misma cuenta
                    int idx;
                    var attempts = 0;
                    do
                    {
                        idx = _rng.Next(defaultComments.Count);
                        attempts++;
                    } while (usedIndices.Contains(idx) && attempts < defaultComments.Count * 2);

                    usedIndices.Add(idx);
                    var phrase = defaultComments[idx].Text;

                    try
                    {
                        var bav = new BotAccountVideo(account.Id, video.Id, phrase)
                        {
                            CommentSent = false,
                            Priority = video.Priority
                        };
                        await _botAccountVideoRepo.CreateAsync(bav);
                        totalCreated++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to create default comment for account {Username} on {VideoId}",
                            account.Username, video.TiktokVideoId);
                    }
                }
            }

            Log.Information("Default comments assigned for {VideoId}: {Count} records created", video.TiktokVideoId, totalCreated);
            return (totalCreated > 0 || accounts.Count == 0, totalCreated);
        }

        private async Task UpdateVideoAsync(VideoRecord video)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
                throw new InvalidOperationException("Video Id is null or empty, cannot update video entity");
            await _videoRepo.UpdateAsync(video.Id, video);
        }
    }
}
