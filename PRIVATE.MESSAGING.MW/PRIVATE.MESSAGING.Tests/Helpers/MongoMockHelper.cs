using MongoDB.Driver;
using Moq;

namespace PRIVATE.MESSAGING.Tests.Helpers;

public static class MongoMockHelper
{
    public static Mock<IMongoCollection<T>> CreateCollectionMock<T>(List<T> data)
    {
        var mockCursor = new Mock<IAsyncCursor<T>>();
        mockCursor.Setup(c => c.Current).Returns(data);
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        var mockCollection = new Mock<IMongoCollection<T>>();
        mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<T>>(),
                It.IsAny<FindOptions<T, T>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        return mockCollection;
    }

    public static Mock<IMongoDatabase> CreateDatabaseMock<T>(string collectionName, Mock<IMongoCollection<T>> mockCollection)
    {
        var mockDb = new Mock<IMongoDatabase>();
        mockDb
            .Setup(d => d.GetCollection<T>(collectionName, It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);
        return mockDb;
    }

    public static Mock<IAsyncCursor<T>> CreateCursor<T>(List<T> data)
    {
        var mockCursor = new Mock<IAsyncCursor<T>>();
        mockCursor.Setup(c => c.Current).Returns(data);
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        return mockCursor;
    }
}
