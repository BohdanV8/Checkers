using System.ComponentModel.DataAnnotations;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Checkers.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        [Required]
        [MaxLength(255)]
        public string PasswordSalt { get; set; } = string.Empty;
        public bool IsActivated { get; set; } = false;

        [MaxLength(255)]
        public string? ActivationLink { get; set; }


        public RefreshToken? RefreshToken { get; set; }
    };
}
