namespace LilyXwfc
{

    /**
     * A connection State is just a symbol.
     * The only operation that can be applied to it is the comparison Equals()
     * Connection state is not a strict requirement of WFC, it is used in the
     * case of rules based on ConnectionStateEntanglementRules
     */
    public struct ConnectionState
    {
        public int[] state; // can have arbitrary dimension

        public bool Equals(ConnectionState other)
        {
            if (state.Length != other.state.Length) return false;
            for (int i = 0; i < state.Length; ++i)
            {
                if (state[i] != other.state[i]) return false;
            }
            return true;
        }

        public override string ToString()
        {
            string s = "ConnectionState(";
            for (int i = 0; i < state.Length; ++i)
            {
                if (i > 0) s += ", ";
                s += state[i];
            }
            s += ")";
            return s;
        }
    }

}
