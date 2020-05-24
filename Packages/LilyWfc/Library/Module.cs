namespace LilyWfc
{

    /**
     * For WFC, a "module" is actually nothing more than a pure state.
     * This class is only used by the entanglement rule set in the case of
     * ConnectionStateEntanglementRules, where rules are based on a connection state.
     * A module imposes certain connection states to its outgoing connections, that
     * are stored in this Module class and queried using GetConnectionState()
     */
    public class Module
    {
        readonly ConnectionState[] connections;

        /**
         * dimension is the number of state components per connection
         */
        public Module(int[] connectionStateValues, int dimension = 3)
        {
            connections = new ConnectionState[connectionStateValues.Length / dimension];
            for (int i = 0; i < connections.Length; ++i)
            {
                connections[i] = new ConnectionState { state = new int[dimension] };
                for (int j = 0; j < dimension; ++j)
                {
                    connections[i].state[j] = connectionStateValues[dimension * i + j];
                }
            }
        }

        public ConnectionState GetConnectionState(int connectionType)
        {
            return connections[connectionType];
        }
    }

}
