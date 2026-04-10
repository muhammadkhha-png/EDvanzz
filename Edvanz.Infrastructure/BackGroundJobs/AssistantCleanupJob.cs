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
            using var scope = _scopeFactory.CreateScope();
            var now = DateTime.UtcNow;

            var assistants = await _uow.AssistantRepo.GetAsync(a =>
                a.AccountStatus == Domain.Enums.AccountStatus.Suspended &&
a.DeletedAt != null &&
a.DeletedAt <= now);

            foreach (var assistant in assistants)
            {
                var user = await _uow.Users.GetUserByIdAsync(assistant.UserId);

                var refreshTokensRepo = _uow.GetRepository<RefreshToken, long>();
                var permRepo = _uow.GetRepository<UsersPermission, long>();
                var templateRepo = _uow.GetRepository<TemplatePermissionsUsers, long>();
                var walletRepo = _uow.GetRepository<AssistantWallet, long>();

                await permRepo.DeleteRangeAsync(await permRepo.GetAsync(x => x.UserId == user.Id));
                await refreshTokensRepo.DeleteRangeAsync(await refreshTokensRepo.GetAsync(x => x.UserId == user.Id));
                await templateRepo.DeleteRangeAsync(await templateRepo.GetAsync(x => x.AssisstantId == assistant.Id));
                await walletRepo.DeleteRangeAsync(await walletRepo.GetAsync(x => x.AssistantId == assistant.Id));

                await _uow.AssistantRepo.DeleteAsync(assistant);
                await _uow.Users.DeleteAsync(user);
            }

            await _uow.SaveChangesAsync();
        }
    }
}
