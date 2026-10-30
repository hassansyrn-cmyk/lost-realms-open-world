namespace LostRealmsOW {
 // Placeholder hook: settlement layouts register their building pads here once planned.
 public static class OpenWorldPlanBridge {
  public static System.Action RegisterSettlementPadsHook;
  public static void RegisterSettlementPads() { RegisterSettlementPadsHook?.Invoke(); }
 }
}
