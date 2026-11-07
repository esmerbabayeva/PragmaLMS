namespace Pragma.Application.DTOs.AiMentor;

public record AiAskRequestDto(
    string Question
);

public record AiAskResponseDto(
    string Answer,
    List<string> SuggestedFollowUps
);

public record AiQuizHintResponseDto(
    string QuestionText,
    string SocraticHint
);
