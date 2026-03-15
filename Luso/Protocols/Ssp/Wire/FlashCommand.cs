// FlashCommand and FlashAction are domain concepts; these aliases keep internal
// usages compiling without the SSP layer needing to reference the domain namespace.
global using FlashCommand = Luso.Features.Rooms.Domain.Commands.FlashCommand;
global using FlashAction = Luso.Features.Rooms.Domain.Commands.FlashAction;
