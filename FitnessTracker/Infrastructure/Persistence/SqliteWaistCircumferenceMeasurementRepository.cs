using FitnessTracker.Application.BodyMeasurements;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Infrastructure.Persistence;

public sealed class SqliteWaistCircumferenceMeasurementRepository(FitnessDatabase database)
    : IWaistCircumferenceMeasurementRepository
{
    public async Task AddAsync(WaistCircumferenceMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        var row = WaistCircumferenceMeasurementRow.FromMeasurement(measurement);
        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>("SELECT COUNT(*) FROM WaistCircumferenceMeasurements WHERE Id = ?", row.Id) > 0)
                throw new InvalidOperationException("Měření se stejným ID už existuje.");
            transaction.Execute("INSERT INTO WaistCircumferenceMeasurements (Id, MeasuredAtUtc, CircumferenceMillimeters) VALUES (?, ?, ?)",
                row.Id, row.MeasuredAtUtc, row.CircumferenceMillimeters);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WaistCircumferenceMeasurement>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        List<WaistCircumferenceMeasurement> measurements = [];
        await connection.RunInTransactionAsync(transaction =>
        {
            measurements = transaction.Query<WaistCircumferenceMeasurementRow>(
                    "SELECT * FROM WaistCircumferenceMeasurements ORDER BY MeasuredAtUtc DESC, Id")
                .Select(row => row.ToMeasurement()).ToList();
        }).ConfigureAwait(false);
        return measurements.AsReadOnly();
    }

    public async Task DeleteAsync(Guid measurementId)
    {
        var connection = await database.GetConnectionAsync().ConfigureAwait(false);
        await connection.RunInTransactionAsync(transaction =>
        {
            int deleted = transaction.Execute("DELETE FROM WaistCircumferenceMeasurements WHERE Id = ?", measurementId.ToString("D"));
            if (deleted == 0) throw new KeyNotFoundException("Měření už není dostupné.");
        }).ConfigureAwait(false);
    }
}
