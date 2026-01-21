namespace Assignment_2_242942m.Services
{
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private readonly int _minPasswordAgeMinutes;
        private readonly int _maxPasswordAgeDays;

        public PasswordPolicyService(IConfiguration config)
        {
            _minPasswordAgeMinutes = config.GetValue<int>("PasswordPolicy:MinPasswordAgeMinutes");
            _maxPasswordAgeDays = config.GetValue<int>("PasswordPolicy:MaxPasswordAgeDays");
        }

        public bool CanChangePassword(DateTime? lastPasswordChange, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (lastPasswordChange == null)
                return true; // First time, allow change

            var timeSinceLastChange = DateTime.UtcNow - lastPasswordChange.Value;
            var minAge = TimeSpan.FromMinutes(_minPasswordAgeMinutes);

            if (timeSinceLastChange < minAge)
            {
                var remaining = minAge - timeSinceLastChange;
                errorMessage = $"You must wait {remaining.Minutes} minute(s) and {remaining.Seconds} second(s) before changing your password again.";
                return false;
            }

            return true;
        }

        public bool MustChangePassword(DateTime? lastPasswordChange)
        {
            if (lastPasswordChange == null)
                return false; // First login, no force yet

            var timeSinceLastChange = DateTime.UtcNow - lastPasswordChange.Value;
            return timeSinceLastChange.TotalDays >= _maxPasswordAgeDays;
        }

        public TimeSpan GetRemainingMinAge(DateTime lastPasswordChange)
        {
            var timeSinceLastChange = DateTime.UtcNow - lastPasswordChange;
            var minAge = TimeSpan.FromMinutes(_minPasswordAgeMinutes);
            return minAge - timeSinceLastChange;
        }
    }
}
