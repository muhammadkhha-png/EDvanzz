using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.BackGroundJobs
{
    public class AssistantCleanupJob
    {
        private readonly IUnitOfWork _uow;
        private readonly IServiceScopeFactory _scopeFactory;


        public AssistantCleanupJob(IUnitOfWork uow, IServiceScopeFactory scopeFactory)
        {
            _uow = uow;
            _scopeFactory = scopeFactory;

        }

        public async Task ExecuteAsync()
        {
            var now = DateTime.UtcNow;

            // Assistants the teacher deleted (soft-delete) that are due for permanent purge.
            var assistants = await _uow.AssistantRepo.GetAsync(a =>
                a.AccountStatus == AccountStatus.Suspended &&
                a.DeletedAt != null &&
                a.DeletedAt <= now);

            foreach (var assistant in assistants)
            {
                // Purge each assistant in its own transaction so one residual FK / bad row
                // never aborts the whole sweep. A row that fails to purge simply stays
                // soft-deleted (hidden from the teacher list) and is retried next run.
                try
                {
                    await _uow.BeginTransactionAsync();

                    var user = await _uow.Users.GetUserByIdAsync(assistant.UserId);

                    var refreshTokensRepo = _uow.GetRepository<RefreshToken, long>();
                    var permRepo = _uow.GetRepository<UsersPermission, long>();
                    var templateRepo = _uow.GetRepository<TemplatePermissionsUsers, long>();
                    var walletRepo = _uow.GetRepository<AssistantWallet, long>();
                    var walletResetLogRepo = _uow.GetRepository<WalletResetLog, long>();
                    var loginLogRepo = _uow.GetRepository<LoginActivityAssistantLog, long>();
                    var notificationRepo = _uow.GetRepository<UserNotification, long>();
                    var deviceTokenRepo = _uow.GetRepository<UserDeviceToken, long>();

                    // ── Assistant-scoped dependents (all NoAction FKs — must be cleared
                    //    before the wallet / assistant rows or the delete is blocked). ──
                    // WalletResetLog references BOTH Assistant and AssistantWallet → delete first.
                    await walletResetLogRepo.DeleteRangeAsync(
                        await walletResetLogRepo.GetAsync(x => x.AssistantId == assistant.Id));
                    await walletRepo.DeleteRangeAsync(
                        await walletRepo.GetAsync(x => x.AssistantId == assistant.Id));
                    await loginLogRepo.DeleteRangeAsync(
                        await loginLogRepo.GetAsync(x => x.AssistantId == assistant.Id));
                    await templateRepo.DeleteRangeAsync(
                        await templateRepo.GetAsync(x => x.AssisstantId == assistant.Id));

                    // ── User-scoped dependents (NoAction FKs to User). ──
                    if (user is not null)
                    {
                        await notificationRepo.DeleteRangeAsync(
                            await notificationRepo.GetAsync(x => x.UserId == user.Id));
                        await deviceTokenRepo.DeleteRangeAsync(
                            await deviceTokenRepo.GetAsync(x => x.UserId == user.Id));
                        await permRepo.DeleteRangeAsync(
                            await permRepo.GetAsync(x => x.UserId == user.Id));
                        await refreshTokensRepo.DeleteRangeAsync(
                            await refreshTokensRepo.GetAsync(x => x.UserId == user.Id));
                    }

                    await _uow.AssistantRepo.DeleteAsync(assistant);
                    if (user is not null)
                        await _uow.Users.DeleteAsync(user);

                    await _uow.SaveChangesAsync();
                    await _uow.CommitAsync();
                }
                catch
                {
                    await _uow.RollbackAsync();
                    // Swallow: keep the row soft-deleted (hidden) and retry on the next sweep.
                }
            }
        }
    }
}
