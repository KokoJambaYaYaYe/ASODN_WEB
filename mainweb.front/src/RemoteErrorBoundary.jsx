import React from 'react';
import { Box, Paper, Typography, Button } from '@mui/material';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import ReplayIcon from '@mui/icons-material/Replay';

export class RemoteErrorBoundary extends React.Component {
    constructor(props) {
        super(props);
        this.state = { hasError: false };

        // КРИТИЧНО: Привязываем контекст this к методу handleRetry
        this.returnToHub = this.returnToHub.bind(this);
    }

    static getDerivedStateFromError(error) {
        return { hasError: true };
    }

    componentDidCatch(error, errorInfo) {
        console.error("Ошибка загрузки удаленного модуля:", error, errorInfo);

        // Уведомляем родительский Хост, что произошла ошибка (чтобы скрыть кнопку "На главную" и переключить интерфейс)
        if (this.props.onErrorStateChange) {
            this.props.onErrorStateChange(true);
        }
    }

    returnToHub() {
        this.setState({ hasError: false });

        // Уведомляем родителя, что ошибка сброшена
        if (this.props.onErrorStateChange) {
            this.props.onErrorStateChange(false);
        }

        // Вызываем переданную функцию возврата на главную страницу
        if (this.props.onNavigateToHub) {
            this.props.onNavigateToHub();
        }
    }

    render() {
        if (this.state.hasError) {
            return (
                <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh', width: '100%', p: 2 }}>
                    <Paper elevation={3} sx={{ p: 4, maxWidth: 550, width: '100%', borderLeft: (theme) => `6px solid ${theme.palette.error.main}`, display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, color: 'error.main' }}>
                            <WarningAmberIcon fontSize="large" />
                            <Typography variant="h5" component="h4" sx={{ fontWeight: 600 }}>
                                Компонент недоступен
                            </Typography>
                        </Box>
                        <Typography variant="body1" color="text.secondary">
                            Не удалось загрузить данный блок системы. Возможно, удаленный микрофронтенд отключен или на сервере ведутся технические работы.
                        </Typography>
                        <Box sx={{ display: 'flex', gap: 2, mt: 1 }}>
                            <Button
                                variant="contained"
                                color="error"
                                size="medium"
                                startIcon={<ReplayIcon />}
                                onClick={this.returnToHub}
                                sx={{ whiteSpace: 'nowrap' }}
                            >
                                Вернуться на главную
                            </Button>
                        </Box>
                    </Paper>
                </Box>
            );
        }

        return this.props.children;
    }
}
