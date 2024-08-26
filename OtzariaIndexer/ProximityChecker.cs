using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OtzariaIndexer
{
    public static class ProximityChecker
    {
        public static List<List<Token>> GetAllValidConsecutiveResults(List<List<Token>> tokenLists, int maxSlop)
        {
            Console.WriteLine("Finding all valid consecutive term sequences within the allowed slop...");

            List<List<Token>> validResults = new List<List<Token>>();

            // Sort all position lists in ascending order
            for (int x = 0; x < tokenLists.Count; x++)
            {
                tokenLists[x] = tokenLists[x].OrderBy(t => t.Position).ToList();
            }

            // Iterate through all possible starting Tokens of the first term
            foreach (var token in tokenLists[0])
            {
                int lastPosition = token.Position;
                List<Token> currentSequence = new List<Token> { token };

                for (int i = 1; i < tokenLists.Count; i++)
                {
                    var nextToken = tokenLists[i].FirstOrDefault(pos => pos.Position >= lastPosition && (pos.Position - lastPosition) <= maxSlop);

                    if (nextToken == null)
                    {
                        break;
                    }

                    currentSequence.Add(nextToken);
                    lastPosition = nextToken.Position;
                }

                if (currentSequence.Count == tokenLists.Count)
                {
                    validResults.Add(new List<Token>(currentSequence));
                }
            }

            Console.WriteLine($"Found {validResults.Count} valid sequences.");

            return validResults;
        }
    }
}
