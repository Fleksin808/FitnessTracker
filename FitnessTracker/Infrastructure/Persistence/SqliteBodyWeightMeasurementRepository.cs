using FitnessTracker.Application.BodyMeasurements;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteBodyWeightMeasurementRepository(FitnessDatabase database)
    : IBodyWeightMeasurementRepository
{
    public async Task AddAsync(BodyWeightMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        var row = BodyWeightMeasurementRow.FromMeasurement(measurement);
        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM BodyWeightMeasurements WHERE Id = ?", row.Id) > 0)
                throw new InvalidOperationException("Měření se stejným ID už existuje.");
            transaction.Execute("INSERT INTO BodyWeightMeasurements (Id, MeasuredAtUtc, WeightGrams) VALUES (?, ?, ?)",
                row.Id, row.MeasuredAtUtc, row.WeightGrams);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BodyWeightMeasurement>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        List<BodyWeightMeasurement> measurements = [];
        await connection.RunInTransactionAsync(transaction =>
        {
            measurements = transaction.Query<BodyWeightMeasurementRow>(
                    "SELECT * FROM BodyWeightMeasurements ORDER BY MeasuredAtUtc DESC, Id")
                .Select(row => row.ToMeasurement()).ToList();
        }).ConfigureAwait(false);
        return measurements.AsReadOnly();
    }

    public async Task DeleteAsync(Guid measurementId)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            int deleted = transaction.Execute("DELETE FROM BodyWeightMeasurements WHERE Id = ?", measurementId.ToString("D"));
            if (deleted == 0) throw new KeyNotFoundException("Měření už není dostupné.");
        }).ConfigureAwait(false);
    }
}
