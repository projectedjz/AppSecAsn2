namespace Assignment_2_242942m.Services
{
    public class PhotoService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;
        public PhotoService(IWebHostEnvironment env, IConfiguration cfg)
        {
            _env = env;
            _cfg = cfg;
        }

        public async Task<string> SavePhotoAsync(IFormFile file)
        {
            if (!file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only JPG allowed");

            // magic-byte
            using var stream = file.OpenReadStream();
            var header = new byte[2];
            await stream.ReadExactlyAsync(header, 0, 2);
            if (header[0] != 0xFF || header[1] != 0xD8)
                throw new ArgumentException("Invalid JPG header");

            // save into wwwroot/Uploads so static middleware can serve it
            var uploads = Path.Combine(_env.WebRootPath, _cfg["PhotoFolder"] ?? "Uploads");
            Directory.CreateDirectory(uploads);
            var fileName = $"{Guid.NewGuid()}.jpg";
            var full = Path.Combine(uploads, fileName);
            stream.Position = 0;
            using var fs = new FileStream(full, FileMode.Create);
            await stream.CopyToAsync(fs);
            return fileName; // store only file name
        }
    }
}
