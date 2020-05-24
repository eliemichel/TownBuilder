namespace LilyXwfc
{

/**
 * This maps a pure state to its module information
 */
public class ModuleRegistry
{
    public Module[] modules;

    public Module GetModule(PureState state)
    {
        return modules[state.index];
    }
}

}