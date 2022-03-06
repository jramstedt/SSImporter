namespace SS {
  public struct Shodan {
    public const int FIRST_SHODAN_QUEST_VAR = 0x10;
    public const int NUM_SHODAN_LEVELS = 13;

    public static int GetShodanQuestVar(int level) => FIRST_SHODAN_QUEST_VAR + level;
  }
}
