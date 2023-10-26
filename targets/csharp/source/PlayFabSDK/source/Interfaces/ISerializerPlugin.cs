namespace PlayFab
{
    /// <summary>
    /// Interface of any data serializer SDK plugin.
    /// </summary>
    public interface ISerializerPlugin : IPlayFabPlugin
    {
        public ValueTask<T?> DeserializeAsync<T>(Stream serialized);
        string Serialize<T>(T obj);
    }
}