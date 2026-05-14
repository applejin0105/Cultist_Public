using Domain.State.Host;
using Effects.Commands.Card;
using Effects.Commands.Flow;
using Effects.Commands.Phase;
using Effects.Commands.Resource;
using Effects.Commands.Turn;
using Effects.Conditions;
using Effects.Interfaces;
using Systems;

namespace Effects.Core
{
    /// <summary>
    /// 명령어/조건의 명시적 등록 + EffectRunner 조립 단일 지점.
    /// (리플렉션 없이 IL2CPP/AOT 친화. 한 파일에서 모든 등록을 한눈에 본다.)
    ///
    /// 사용법:
    ///   var bootstrap = new EffectsBootstrap(gameState, actionSystem, input, rng, phase);
    ///   actionSystem.SetEffectRunner(bootstrap.Runner);
    /// </summary>
    public sealed class EffectsBootstrap
    {
        public CommandRegistry Commands { get; }
        public ConditionRegistry Conditions { get; }
        public TargetResolver Targets { get; }
        public EffectRunner Runner { get; }

        public EffectsBootstrap(
            GameState gameState,
            GameActionSystem actionSystem,
            IPlayerInputProvider input,
            IRandomSource rng,
            PhaseSystem phaseSystem,
            TurnSystem turnSystem,
            GameRuleSystem gameRuleSystem)
        {
            Commands = new CommandRegistry();
            Conditions = new ConditionRegistry();
            Targets = new TargetResolver(gameState, input, rng);

            // ===== 명령어 등록 =====
            // Step 2
            Commands.Register("Log", new LogCommand());
            Commands.Register("Get", new GetCommand(gameState, Targets));
            // Step 3
            Commands.Register("Phase", new PhaseCommand(phaseSystem, Targets));
            Commands.Register("SetNextDraw", new SetNextDrawCommand(gameState, Targets));
            Commands.Register("Draw", new DrawCommand(actionSystem, Targets));
            // Step 4
            Commands.Register("Destroy", new DestroyCommand(gameState, actionSystem, Targets, input, rng));
            Commands.Register("Exile", new ExileCommand(gameState, actionSystem, Targets, input, rng));
            // Step 5+: Sacrifice / Trade / Starve
            Commands.Register("Sacrifice", new SacrificeCommand(gameState, actionSystem, Targets, input, rng));
            Commands.Register("Trade",     new TradeCommand(actionSystem, Targets));
            Commands.Register("Starve",    new StarveCommand(actionSystem, Targets));
            Commands.Register("AddTurnCycle", new AddTurnCycleCommand(gameState, turnSystem));
            // Step 6+: If
            Commands.Register("If", new IfCommand(Conditions));
            Commands.Register("SetVar", new SetVarCommand(Targets));
            // Step 7+: Reveal / Cancel
            Commands.Register("Reveal", new RevealCommand(actionSystem, Targets, rng));
            Commands.Register("Cancel", new CancelCommand());

            // ===== 조건 등록 (Step 6부터 채움) =====
            Conditions.Register("HasSymbol", new HasSymbolCondition());
            Conditions.Register("HasCultist", new HasCultistCondition());
            Conditions.Register("HasCard", new HasCardCondition());
            Conditions.Register("Compare", new CompareCondition());

            actionSystem.StatSystem.SetTargetResolver(Targets);
            actionSystem.StatSystem.SetConditionRegistry(Conditions);

            Runner = new EffectRunner(gameState, Commands);
            Runner.SetGameRuleSystem(gameRuleSystem);
        }
    }
}