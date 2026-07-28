import React from 'react';
import { Box, Paper, Typography, Button } from '@mui/material';
import BarChartIcon from '@mui/icons-material/BarChart';

export default function HubScreenCom({ activeModule, onNavigate }) {
    if (activeModule !== 'hub') return null;

    return (
        <Paper elevation={3} className="hub-paper">
            <Typography variant="h5" className="hub-title">
                Доступные модули системы
            </Typography>
            <Typography variant="body2" className="hub-subtitle">
                Выберите необходимый модуль для начала работы
            </Typography>
            <Box className="button-stack">
                <Button
                    variant="contained"
                    size="large"
                    startIcon={<BarChartIcon />}
                    onClick={() => onNavigate('svod_reports')}
                    className="reports-nav-button"
                >
                    Сводная отчетность
                </Button>
            </Box>
        </Paper>
    );
}
