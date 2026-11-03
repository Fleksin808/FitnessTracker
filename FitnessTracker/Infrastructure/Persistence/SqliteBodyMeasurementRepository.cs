using FitnessTracker.Application.BodyMeasurements;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteBodyMeasurementRepository(FitnessDatabase database) : IBodyMeasurementRepository
{
    public async Task AddAsync(BodyMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        var row = BodyMeasurementRow.FromMeasurement(measurement);
        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM BodyMeasurements WHERE Id = ?", row.Id) > 0)
                throw new InvalidOperationException("Měření se stejným ID už existuje.");
            transaction.Execute("INSERT INTO BodyMeasurements (Id, BodyPart, MeasuredAtUtc, CircumferenceMillimeters) VALUES (?, ?, ?, ?)",
                row.Id, row.BodyPart, row.MeasuredAtUtc, row.CircumferenceMillimeters);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BodyMeasurement>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        List<BodyMeasurement> measurements = [];
        await connection.RunInTransactionAsync(transaction =>
        {
            measurements = transaction.Query<BodyMeasurementRow>(
                    "SELECT * FROM BodyMeasurements ORDER BY MeasuredAtUtc DESC, Id")
                .Select(row => row.ToMeasurement()).ToList();
        }).ConfigureAwait(false);
        return measurements.AsReadOnly();
    }

    public async Task DeleteAsync(Guid measurementId)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            int deleted = transaction.Execute("DELETE FROM BodyMeasurements WHERE Id = ?", measurementId.ToString("D"));
            if (deleted == 0) throw new KeyNotFoundException("Měření už není dostupné.");
        }).ConfigureAwait(false);
    }
}
