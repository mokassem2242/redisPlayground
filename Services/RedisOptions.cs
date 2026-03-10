namespace redisPlayground.Services;

public class RedisOptions
{
    public const string SectionName = "Redis";
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "RedisPlayground:";
}
