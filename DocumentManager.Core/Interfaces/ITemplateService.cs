using DocumentManager.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Interfaces
{
    /// <summary>
    /// Сервис для работы с шаблонами документов
    /// </summary>
    public interface ITemplateService
    {
        /// <summary>
        /// Получить все шаблоны документов
        /// </summary>
        Task<IEnumerable<DocumentTemplate>> GetAllTemplatesAsync();

        /// <summary>
        /// Получить шаблон документа по идентификатору
        /// </summary>
        Task<DocumentTemplate> GetTemplateByIdAsync(int id);

        /// <summary>
        /// Получить шаблон документа по коду
        /// </summary>
        Task<DocumentTemplate> GetTemplateByCodeAsync(string code);

        /// <summary>
        /// Получить поля шаблона документа
        /// </summary>
        Task<IEnumerable<DocumentField>> GetTemplateFieldsAsync(int templateId);

        /// <summary>
        /// Загрузить определение полей из JSON-файла
        /// </summary>
        Task<IEnumerable<DocumentField>> LoadFieldsFromJsonAsync(string jsonPath);

        /// <summary>
        /// Добавить шаблон документа
        /// </summary>
        Task<DocumentTemplate> AddTemplateAsync(DocumentTemplate template);

        /// <summary>
        /// Обновить шаблон документа
        /// </summary>
        Task<bool> UpdateTemplateAsync(DocumentTemplate template);

        /// <summary>
        /// Удалить шаблон документа
        /// </summary>
        Task<bool> DeleteTemplateAsync(int id);
    }

}