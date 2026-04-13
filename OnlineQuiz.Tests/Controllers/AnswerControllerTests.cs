using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineQuiz.Controllers;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;
using System.Security.Claims;
using Xunit;

namespace OnlineQuiz.Tests.Controllers
{
    internal class FakeAnswerService : IAnswerService
    {
        public bool ThrowUnauthorized { get; set; }
        public bool ThrowInvalidIds { get; set; }

        public Task<AnswerResponseDto> RecordAnswerAsync(CreateAnswerDto createAnswerDto, int studentId)
        {
            if (ThrowUnauthorized)
                throw new UnauthorizedAccessException("Not authorized to record answer");
            if (ThrowInvalidIds)
                throw new ArgumentException("Invalid question or choice ID");

            return Task.FromResult(new AnswerResponseDto
            {
                AnswerId = 1,
                AttemptId = createAnswerDto.AttemptId,
                QuestionId = createAnswerDto.QuestionId,
                ChoiceId = createAnswerDto.ChoiceId,
                TextAnswer = createAnswerDto.TextAnswer,
                AnsweredAt = DateTime.UtcNow,
                IsCorrect = null
            });
        }

        public Task<List<AnswerResponseDto>> RecordBulkAnswersAsync(BulkAnswerRequestDto bulkAnswerDto, int studentId)
        {
            if (ThrowUnauthorized)
                throw new UnauthorizedAccessException("Not authorized to record answers");
            if (ThrowInvalidIds)
                throw new ArgumentException("Invalid question IDs in bulk submission");

            var list = bulkAnswerDto.Answers.Select((a, idx) => new AnswerResponseDto
            {
                AnswerId = idx + 1,
                AttemptId = bulkAnswerDto.AttemptId,
                QuestionId = a.QuestionId,
                ChoiceId = a.ChoiceId,
                TextAnswer = a.TextAnswer,
                AnsweredAt = DateTime.UtcNow
            }).ToList();
            return Task.FromResult(list);
        }

        public Task<AttemptWithAnswersDto> GetAnswersForAttemptAsync(int attemptId, int userId)
        {
            // Return empty answers collection in wrapper dto to satisfy interface
            return Task.FromResult(new AttemptWithAnswersDto
            {
                AttemptId = attemptId,
                UserId = userId,
                Answers = new List<AnswerResponseDto>()
            });
        }

        public Task<AnswerResponseDto> UpdateAnswerAsync(int answerId, CreateAnswerDto updateAnswerDto, int studentId)
        {
            if (ThrowUnauthorized)
                throw new UnauthorizedAccessException("Not authorized to update answer");
            if (ThrowInvalidIds)
                throw new ArgumentException("Invalid question or choice ID");

            return Task.FromResult(new AnswerResponseDto
            {
                AnswerId = answerId,
                AttemptId = updateAnswerDto.AttemptId,
                QuestionId = updateAnswerDto.QuestionId,
                ChoiceId = updateAnswerDto.ChoiceId,
                TextAnswer = updateAnswerDto.TextAnswer,
                AnsweredAt = DateTime.UtcNow
            });
        }

        public Task<bool> DeleteAnswerAsync(int answerId, int studentId)
        {
            if (ThrowUnauthorized)
                throw new UnauthorizedAccessException("Not authorized to delete answer");
            if (ThrowInvalidIds)
                throw new ArgumentException("Answer not found");
            return Task.FromResult(true);
        }
    }

    public class AnswerControllerTests
    {
        private static AnswerController CreateController(FakeAnswerService service)
        {
            var controller = new AnswerController(service);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "99")
                    }, "TestAuth"))
                }
            };
            return controller;
        }

        [Fact]
        public async Task RecordAnswer_ReturnsCreated_WhenValid()
        {
            var service = new FakeAnswerService();
            var controller = CreateController(service);
            var dto = new CreateAnswerDto { AttemptId = 10, QuestionId = 100, ChoiceId = 200 };

            var result = await controller.RecordAnswer(dto, studentId: 99);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var response = Assert.IsType<AnswerResponseDto>(created.Value);
            Assert.Equal(10, response.AttemptId);
            Assert.Equal(100, response.QuestionId);
            Assert.Equal(200, response.ChoiceId);
        }

        [Fact]
        public async Task RecordAnswer_ReturnsBadRequest_OnInvalidQuestionOrChoice()
        {
            var service = new FakeAnswerService { ThrowInvalidIds = true };
            var controller = CreateController(service);
            var dto = new CreateAnswerDto { AttemptId = 11, QuestionId = 999, ChoiceId = 9999 };

            var result = await controller.RecordAnswer(dto, studentId: 99);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var payload = bad.Value as dynamic;
            Assert.NotNull(payload);
        }

        [Fact]
        public async Task RecordBulkAnswers_ReturnsOk_WhenValid()
        {
            var service = new FakeAnswerService();
            var controller = CreateController(service);
            var bulk = new BulkAnswerRequestDto
            {
                AttemptId = 77,
                Answers = new List<AnswerSubmissionDto>
                {
                    new AnswerSubmissionDto { QuestionId = 1, ChoiceId = 2 },
                    new AnswerSubmissionDto { QuestionId = 3, TextAnswer = "Hello" }
                }
            };

            var result = await controller.RecordBulkAnswers(bulk, studentId: 99);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<AnswerResponseDto>>(ok.Value);
            Assert.Equal(2, list.Count);
            Assert.All(list, a => Assert.Equal(77, a.AttemptId));
        }

        [Fact]
        public async Task RecordBulkAnswers_ReturnsNotFound_OnInvalidQuestionIds()
        {
            var service = new FakeAnswerService { ThrowInvalidIds = true };
            var controller = CreateController(service);
            var bulk = new BulkAnswerRequestDto
            {
                AttemptId = 88,
                Answers = new List<AnswerSubmissionDto>
                {
                    new AnswerSubmissionDto { QuestionId = 9999, ChoiceId = 2 },
                }
            };

            var result = await controller.RecordBulkAnswers(bulk, studentId: 99);

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            var payload = notFound.Value as dynamic;
            Assert.NotNull(payload);
        }

        [Fact]
        public async Task GetAnswersForAttempt_ReturnsEmptyList_WhenNoAnswers()
        {
            var service = new FakeAnswerService();
            var controller = CreateController(service);

            var result = await controller.GetAnswersForAttempt(attemptId: 123, userId: 99);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AttemptWithAnswersDto>(ok.Value);
            Assert.Equal(123, dto.AttemptId);
            Assert.Equal(99, dto.UserId);
            Assert.Empty(dto.Answers);
        }
    }
}
