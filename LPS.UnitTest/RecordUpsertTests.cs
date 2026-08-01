using System.Collections.Generic;
using System.Linq;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.LPSCommandLine.Commands;
using Xunit;

namespace LPS.UnitTest
{
    public class RecordUpsertTests
    {
        private static HttpIterationDto Iteration(string name, string method, string url, string raw = null)
        {
            return new HttpIterationDto
            {
                Name = name,
                HttpRequest = new HttpRequestDto
                {
                    HttpMethod = method,
                    URL = url,
                    Payload = raw is null ? null : new PayloadDto { Raw = raw }
                }
            };
        }

        private static PlanDto PlanWith(string roundName, params HttpIterationDto[] iterations)
        {
            return new PlanDto
            {
                Name = "P",
                Rounds = new List<RoundDto>
                {
                    new RoundDto { Name = roundName, Iterations = iterations.ToList() }
                }
            };
        }

        [Fact]
        public void Upsert_ExistingRequest_IsUpdatedInPlace_NotDuplicated()
        {
            var plan = PlanWith("Browse", Iteration("GET_products_1", "GET", "https://api/products/1"));
            var incoming = Iteration("GET_products_1_new", "GET", "https://api/products/1");

            var (updated, added) = RecordCliCommand.UpsertIterations(plan, "Browse", new List<HttpIterationDto> { incoming });

            Assert.Equal(1, updated);
            Assert.Equal(0, added);
            var round = plan.Rounds.Single();
            Assert.Single(round.Iterations);
            // Existing name is preserved; the request object is refreshed to the incoming one.
            Assert.Equal("GET_products_1", round.Iterations[0].Name);
            Assert.Same(incoming.HttpRequest, round.Iterations[0].HttpRequest);
        }

        [Fact]
        public void Upsert_NewRequest_IsAddedToTargetRound()
        {
            var plan = PlanWith("Browse", Iteration("GET_products_1", "GET", "https://api/products/1"));
            var incoming = Iteration("GET_products_2", "GET", "https://api/products/2");

            var (updated, added) = RecordCliCommand.UpsertIterations(plan, "Browse", new List<HttpIterationDto> { incoming });

            Assert.Equal(0, updated);
            Assert.Equal(1, added);
            Assert.Equal(2, plan.Rounds.Single().Iterations.Count);
        }

        [Fact]
        public void Upsert_SameUrlDifferentBody_IsTreatedAsDistinct()
        {
            var plan = PlanWith("Api", Iteration("graphql", "POST", "https://api/graphql", raw: "{\"query\":\"A\"}"));
            var incoming = Iteration("graphql_b", "POST", "https://api/graphql", raw: "{\"query\":\"B\"}");

            var (updated, added) = RecordCliCommand.UpsertIterations(plan, "Api", new List<HttpIterationDto> { incoming });

            Assert.Equal(0, updated);
            Assert.Equal(1, added);
            Assert.Equal(2, plan.Rounds.Single().Iterations.Count);
        }

        [Fact]
        public void Upsert_CollapsesDuplicatesWithinTheSameBatch()
        {
            var plan = PlanWith("Browse");
            var batch = new List<HttpIterationDto>
            {
                Iteration("GET_a", "GET", "https://api/a"),
                Iteration("GET_a_again", "GET", "https://api/a")
            };

            var (updated, added) = RecordCliCommand.UpsertIterations(plan, "Browse", batch);

            Assert.Equal(1, added);
            Assert.Equal(1, updated);
            Assert.Single(plan.Rounds.Single().Iterations);
        }

        [Fact]
        public void Upsert_MatchIsFoundAcrossAnyRound()
        {
            var plan = new PlanDto
            {
                Name = "P",
                Rounds = new List<RoundDto>
                {
                    new RoundDto { Name = "Browse", Iterations = new List<HttpIterationDto> { Iteration("home", "GET", "https://api/") } },
                    new RoundDto { Name = "Checkout", Iterations = new List<HttpIterationDto> { Iteration("pay", "POST", "https://api/pay", raw: "{}") } }
                }
            };
            var incoming = Iteration("pay_new", "POST", "https://api/pay", raw: "{}");

            var (updated, added) = RecordCliCommand.UpsertIterations(plan, "Browse", new List<HttpIterationDto> { incoming });

            Assert.Equal(1, updated);
            Assert.Equal(0, added);
            // Updated in place inside "Checkout", not moved to the target round.
            Assert.Single(plan.Rounds.First(r => r.Name == "Checkout").Iterations);
            Assert.Single(plan.Rounds.First(r => r.Name == "Browse").Iterations);
        }

        [Fact]
        public void Upsert_CreatesTargetRound_WhenMissing()
        {
            var plan = new PlanDto { Name = "P", Rounds = new List<RoundDto>() };
            var incoming = Iteration("GET_a", "GET", "https://api/a");

            var (updated, added) = RecordCliCommand.UpsertIterations(plan, "Main", new List<HttpIterationDto> { incoming });

            Assert.Equal(0, updated);
            Assert.Equal(1, added);
            var round = Assert.Single(plan.Rounds);
            Assert.Equal("Main", round.Name);
            Assert.Single(round.Iterations);
        }
    }
}
