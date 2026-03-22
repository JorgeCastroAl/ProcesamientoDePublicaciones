using FluxAnswer.Models;
using PocketBase.Framework;
using PocketBase.Framework.Attributes;
using PocketBase.Framework.Repository;

namespace FluxAnswer.Repositories
{
    [CollectionName("default_comments")]
    public class DefaultCommentRepo : BaseRepository<DefaultComment>, IDefaultCommentRepo
    {
        public DefaultCommentRepo(PocketBaseOptions options) : base(options) { }
    }
}
