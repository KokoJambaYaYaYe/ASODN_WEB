import React from 'react';
import { Box, Button } from '@mui/material';
import HomeIcon from '@mui/icons-material/Home';

export default function FloatingHomeButtonCom({ activeModule, hasError, onNavigate }) {
    if (activeModule === 'hub' || hasError) return null;

    return (
        <Box className="floating-home-box">
            <Button
                variant="contained"
                color="primary"
                startIcon={<HomeIcon />}
                onClick={() => onNavigate('hub')}
                className="home-button"
            >
                На главную
            </Button>
        </Box>
    );
}
