namespace NeighborGoods.Api.Features.Auth.Services;

public interface ILineOAuthStateStore
{
    string Create();
    bool Consume(string state);
}
