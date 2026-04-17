namespace AnimalTracker.Data.Entities;

public enum PhotoImportItemStatus
{
    Pending = 0,
    SkippedDuplicate = 1,
    Created = 2,
    Failed = 3,
    NeedsReview = 4
}
