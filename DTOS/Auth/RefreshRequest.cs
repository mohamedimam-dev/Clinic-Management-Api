namespace ClinicManagementApi.DTOS.Auth
{
    public class RefreshRequest
    {
        public string RefreshToken { get; set; }
        public string UserName { get; set; }
    }
}
