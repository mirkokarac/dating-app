using API.Interfaces;

namespace API.Data;

public class UnitOfWork(IUserRepository userRepo, IMessageRepository messageRepo,
ILikesRepository likesRepo, DataContext context) : IUnitOfWork
{
    public IUserRepository UserRepository => userRepo;

    public IMessageRepository MessageRepository => messageRepo;

    public ILikesRepository LikesRepository => likesRepo;

    public async Task<bool> Complete()
    {
        return await context.SaveChangesAsync() > 0;
    }

    public bool HasChanges()
    {
        return context.ChangeTracker.HasChanges();
    }
}
