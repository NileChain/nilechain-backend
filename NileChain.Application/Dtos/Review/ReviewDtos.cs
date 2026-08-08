namespace NileChain.Application.Dtos.Review;

public class CreateReviewRequest
{
    public Guid ContractId { get; set; }
    public Guid TargetId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid TargetId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
