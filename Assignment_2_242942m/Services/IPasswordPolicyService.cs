namespace Assignment_2_242942m.Services
{
    public interface IPasswordPolicyService
    {
        bool CanChangePassword(DateTime? lastPasswordChange, out string errorMessage);
        bool MustChangePassword(DateTime? lastPasswordChange);
        TimeSpan GetRemainingMinAge(DateTime lastPasswordChange);
    }
}
