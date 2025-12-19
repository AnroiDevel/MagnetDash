public interface IMagneticNodeListener
{
    void AddNode(MagneticNode node);
    void OnNodeTriggerExit(MagneticNode node);
    void RegisterVisitedNode(MagneticNode node);
}
