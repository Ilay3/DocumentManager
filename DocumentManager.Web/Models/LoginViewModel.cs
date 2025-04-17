using System.ComponentModel.DataAnnotations;

namespace DocumentManager.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Введите ваше ФИО")]
        [Display(Name = "ФИО")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "ФИО должно содержать от 3 до 100 символов")]
        public string FullName { get; set; }
    }
}
