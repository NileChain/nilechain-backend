namespace NileChain.Application.Dtos.Admin
{
    public class UpdateUserRequest
    {
        public string? Name { get; set; }
        public string? Role { get; set; }
        public bool? IsVerified { get; set; }
        public string? Governorate { get; set; }
        public decimal? SizeInFeddans { get; set; }
    }
}
