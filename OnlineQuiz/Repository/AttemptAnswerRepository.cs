using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using Postgrest;

namespace OnlineQuiz.Repository
{
    public class AttemptAnswerRepository : IAttemptAnswerRepository
    {
        private readonly SupabaseService _supabaseService;

        public AttemptAnswerRepository(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public async Task<AttemptAnswer> CreateAsync(AttemptAnswer answer)
        {
            var client = _supabaseService.GetClient();
            var options = new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation };
            var result = await client.From<AttemptAnswer>().Insert(answer, options);
            var created = result.Models.First();
            
            // If AttemptAnswerId is not populated, fetch by AttemptId and QuestionId
            if (created.AttemptAnswerId == 0)
            {
                var fetchResult = await client.From<AttemptAnswer>()
                    .Where(a => a.AttemptId == answer.AttemptId && a.QuestionId == answer.QuestionId)
                    .Get();
                return fetchResult.Models.FirstOrDefault() ?? created;
            }
            
            return created;
        }

        public async Task<AttemptAnswer?> GetByIdAsync(int answerId)
        {
            var client = _supabaseService.GetClient();
            var result = await client.From<AttemptAnswer>()
                .Where(a => a.AttemptAnswerId == answerId)
                .Get();
            return result.Models.FirstOrDefault();
        }

        public async Task<List<AttemptAnswer>> GetByAttemptIdAsync(int attemptId)
        {
            var client = _supabaseService.GetClient();
            var result = await client.From<AttemptAnswer>()
                .Where(a => a.AttemptId == attemptId)
                .Get();
            return result.Models;
        }

        public async Task<List<AttemptAnswer>> GetByAttemptIdsAsync(List<int> attemptIds)
        {
            if (!attemptIds.Any())
            {
                return new List<AttemptAnswer>();
            }

            var client = _supabaseService.GetClient();
            var result = await client.From<AttemptAnswer>()
                .Filter("AttemptId", Postgrest.Constants.Operator.In, attemptIds)
                .Get();
            return result.Models;
        }

        public async Task<AttemptAnswer> UpdateAsync(AttemptAnswer answer)
        {
            var client = _supabaseService.GetClient();
            var result = await client.From<AttemptAnswer>().Update(answer);
            return result.Models.First();
        }

        public async Task<bool> DeleteAsync(int answerId)
        {
            var client = _supabaseService.GetClient();
            await client.From<AttemptAnswer>()
                .Where(a => a.AttemptAnswerId == answerId)
                .Delete();
            return true;
        }
    }
}
