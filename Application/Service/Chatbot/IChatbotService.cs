using Application.DTOs.Request.Chatbot;
using Application.DTOs.Response.Chatbot;

namespace Application.Service.Chatbot
{
    public interface IChatbotService
    {
        Task<ChatMessageResponse> GetChatResponseAsync(string userMessage);
        
    }
}
