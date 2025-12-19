public interface ISkinDatabase
{
    DroneSkinDefinition GetById(string id);
    DroneSkinDefinition[] GetAll();
}
