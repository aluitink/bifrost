using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bifrost.Public.Sdk.Models;
using Hik.Communication.ScsServices.Service;

namespace Bifrost.Public.Sdk.Interfaces
{
    [ScsService]
    public interface INodeService
    {
        IEnumerable<Node> RetrieveNodes();
        void SendNodes(IEnumerable<Node> nodes);
        bool RequestCircuit(Circuit circuit, string callbackAddress);
    }
}
