using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Infrastructure.BackGroundJobs
{
    public class AssistantCleanupJob
    {
        // Fields retained so the DI registration / scheduled-job signature is unchanged.
        private readonly IUnitOfWork _uow;
        private readonly IServiceScopeFactory _scopeFactory;

        public AssistantCleanupJob(IUnitOfWork uow, IServiceScopeFactory scopeFactory)
        {
            _uow = uow;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// No-op. Soft-delete of an assistant is now TERMINAL: the deleted assistant is
        /// KEPT (hidden from the active list, login blocked via SecurityStamp/Redis on
        /// delete) so their wallet, handover history (WalletResetLog), and the collections
        /// attributed to them stay visible for tracking. The previous hard-purge deleted
        /// held-cash records and orphaned the collector attribution on every transaction
        /// they had taken. Delete is also blocked upstream while the wallet still holds
        /// cash (AssistantService.SoftDeleteAssistantAsync), so no cash is ever lost.
        /// </summary>
        public Task ExecuteAsync() => Task.CompletedTask;
    }
}
