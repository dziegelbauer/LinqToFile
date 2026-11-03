using LinqToFile.Infrastructure;

namespace LinqToFile;

/// <summary>A generated mapping contract. The generator implements this interface on marked partial classes.</summary>
/// <typeparam name="TSelf">The mapped record class.</typeparam>
public interface ITabularRecord<TSelf> where TSelf : class, ITabularRecord<TSelf>
{
    static abstract RecordMapper<TSelf> Mapper { get; }
}
