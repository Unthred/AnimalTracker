namespace AnimalTracker.Services;

public interface IAnimalRecognitionService
{
    Task<RecognitionResponse?> RecognizeAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
}
