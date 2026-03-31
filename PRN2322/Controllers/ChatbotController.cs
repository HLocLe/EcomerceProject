using Application.DTOs.Request.Chatbot;
using Application.DTOs.Response;
using Application.DTOs.Response.Chatbot;
using Application.Service.Chatbot;
using Microsoft.AspNetCore.Mvc;

namespace PRN2322.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

     
        [HttpPost("chat")]
        public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> Chat([FromBody] ChatMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(ApiResponse<ChatMessageResponse>.FailureResponse("Message is required."));
            }

            try
            {
                var result = await _chatbotService.GetChatResponseAsync(request.Message.Trim());
                return Ok(ApiResponse<ChatMessageResponse>.SuccessResponse(result, "Chat response retrieved successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ChatMessageResponse>.FailureResponse($"Chat failed: {ex.Message}"));
            }
        }

    }
}
