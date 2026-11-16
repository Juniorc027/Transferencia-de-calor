namespace TransferenciaCalor.Simulation
{
    /// <summary>
    /// Contrato comum aos motores de simulação (N1 sequencial e N2 paralelo).
    /// Existe para que a UI, o gravador de banco e o benchmark troquem de motor
    /// sem saber qual implementação está por trás — e sem alterar o motor da N1.
    /// </summary>
    public interface IMotorSimulacao
    {
        /// <summary>Grade de corpos que o motor está simulando.</summary>
        HeatGrid Grade { get; }

        /// <summary>Δt (s) de cada tick.</summary>
        double IntervaloTempo { get; }

        /// <summary>Avança a simulação em um tick.</summary>
        void Tick();

        /// <summary>True quando o calor sensível dos vizinhos já se igualou (critério da N1).</summary>
        bool AtingiuEquilibrio();
    }
}
