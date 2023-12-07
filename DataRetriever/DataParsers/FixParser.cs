using System;

namespace VisualHFT.DataRetriever.DataParsers;

public class FixParser : IDataParser
{
    public T Parse<T>(string rawData)
    {
        // Implement FIX message parsing logic here
        // Convert the FIX message to the desired model and return

        throw new NotImplementedException();
    }

    public T Parse<T>(string rawData, dynamic settings)
    {
        throw new NotImplementedException();
    }
}